using System.Windows.Threading;
using ScreenDarkFilter.Color;
using ScreenDarkFilter.Native;
using ScreenDarkFilter.Overlay;

namespace ScreenDarkFilter.Services;

internal sealed class FilterService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<IntPtr, MagnifierHost> _overlays = [];
    private readonly Dictionary<IntPtr, long> _overlayLostAt = [];
    private MagnifierHost? _monitorOverlay;
    private MagColorEffect _identity;
    private bool _fullscreenActive;
    private bool _started;
    private long _lastFrontCheck;
    private bool _cachedOnFront;
    private int _frontOffStreak;
    private MagColorEffect _lastFullscreenEffect;
    private bool _fullscreenEffectReady;
    private string _status = "Фильтр выключен.";

    private MagColorEffect _cachedMatrix;
    private bool _matrixReady;
    private long _lastFindTick;
    private IReadOnlyList<OneCWindow> _cachedWindows = [];

    public FilterService(AppSettings settings)
    {
        _settings = settings;
        _identity = ColorMatrix5.Identity().ToNative();
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _timer.Tick += (_, _) => Tick();
        _settings.Changed += (_, _) =>
        {
            _matrixReady = false;
            SettingsStore.Save(_settings);
            StartupService.Apply(_settings.StartWithWindows);
        };
    }

    public AppSettings Settings => _settings;
    public string Status => _status;
    public event EventHandler? StatusChanged;

    public Func<IReadOnlyList<IntPtr>>? GetChromeWindows { get; set; }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        OneCUiWatcher.Start(OnOneCUi);
        _timer.Start();
        Tick();
    }

    public void Toggle()
    {
        _settings.Enabled = !_settings.Enabled;
    }

    public void Disable()
    {
        _settings.Enabled = false;
        ResetNow();
        SyncFontSmoothing(filterOn: false);
    }

    public static void EmergencyReset()
    {
        FontSmoothingService.Restore();
        NativeMethods.MagInitialize();
        var identity = ColorMatrix5.Identity().ToNative();
        NativeMethods.MagSetFullscreenColorEffect(ref identity);
    }

    public void Dispose()
    {
        _timer.Stop();
        OneCUiWatcher.Stop();
        ResetNow();
        SyncFontSmoothing(filterOn: false);
    }

    private void Tick()
    {
        try
        {
            Apply();
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка фильтра: " + ex.Message);
        }
    }

    private void Apply()
    {
        bool wantOn = _settings.Enabled;
        if (wantOn && _settings.OnlyWhenOneCActive && !IsOneCOnFront())
        {
            wantOn = false;
        }

        SyncFontSmoothing(wantOn);

        if (!wantOn)
        {
            if (_settings.Enabled && _settings.OnlyWhenOneCActive)
            {
                ClearFullscreen();
                HideOverlays();
            }
            else
            {
                ResetNow();
            }

            SetStatus(_settings.Enabled
                ? "1С не на переднем плане — фильтр скрыт. Окно 1С должно быть видно и не перекрыто."
                : "Фильтр выключен. Ctrl+Alt+D включает.");
            return;
        }

        var matrix = GetMatrix();
        var exclude = BuildExcludeList();

        switch (_settings.Mode)
        {
            case FilterMode.Desktop:
                HideOverlays();
                if (!_fullscreenEffectReady || !_lastFullscreenEffect.EqualsEffect(matrix))
                {
                    if (NativeMethods.MagSetFullscreenColorEffect(ref matrix))
                    {
                        _lastFullscreenEffect = matrix;
                        _fullscreenEffectReady = true;
                        _fullscreenActive = true;
                    }
                    else
                    {
                        SetStatus("Не удалось применить полноэкранный фильтр. Закройте штатную Лупу Windows.");
                        break;
                    }
                }

                _fullscreenActive = true;
                SetStatus("Фильтр на всём рабочем столе.");
                break;

            case FilterMode.Monitor:
                ClearFullscreen();
                ApplyMonitor(matrix, exclude);
                break;

            default:
                ClearFullscreen();
                ApplyOneC(matrix, exclude);
                break;
        }

        RaiseChrome(exclude);
    }

    private MagColorEffect GetMatrix()
    {
        if (_matrixReady)
        {
            return _cachedMatrix;
        }

        _cachedMatrix = ThemeEngine.Build(_settings).ToNative();
        _matrixReady = true;
        return _cachedMatrix;
    }

    private IReadOnlyList<OneCWindow> GetOneCWindows()
    {
        long now = Environment.TickCount64;
        if (now - _lastFindTick < 300 && _cachedWindows.Count > 0)
        {
            var updated = new List<OneCWindow>(_cachedWindows.Count);
            foreach (var window in _cachedWindows)
            {
                if (!NativeMethods.IsWindow(window.Handle)
                    || NativeMethods.IsIconic(window.Handle)
                    || NativeMethods.IsCloaked(window.Handle))
                {
                    continue;
                }

                if (!NativeMethods.TryGetVisibleBounds(window.Handle, out RECT bounds)
                    || bounds.Width < 120
                    || bounds.Height < 80)
                {
                    continue;
                }

                updated.Add(window with { Bounds = bounds });
            }

            if (updated.Count > 0)
            {
                _cachedWindows = updated;
                UpdateOneCPids();
                return _cachedWindows;
            }
        }

        _lastFindTick = now;
        _cachedWindows = OneCWindowFinder.Find(_settings.IncludeThickClient);
        UpdateOneCPids();
        return _cachedWindows;
    }

    private void UpdateOneCPids()
    {
        var pids = new HashSet<uint>();
        foreach (var window in _cachedWindows)
        {
            NativeMethods.GetWindowThreadProcessId(window.Handle, out uint pid);
            if (pid != 0)
            {
                pids.Add(pid);
            }
        }

        OneCUiWatcher.SetPids(pids);
    }

    private void OnOneCUi(bool zOrder)
    {
        if (!_started || !_settings.Enabled || _settings.Mode != FilterMode.OneC)
        {
            return;
        }

        if (zOrder)
        {
            RestackOneCOverlays();
        }
    }

    private void RestackOneCOverlays()
    {
        foreach (var window in _cachedWindows)
        {
            if (!_overlays.TryGetValue(window.Handle, out var overlay))
            {
                continue;
            }

            IntPtr above = NativeMethods.GetWindow(window.Handle, NativeConstants.GwHwndPrev);
            if (above == overlay.HostHandle)
            {
                above = NativeMethods.GetWindow(above, NativeConstants.GwHwndPrev);
            }

            overlay.Restack(above, window.Handle);
        }
    }

    private bool IsOneCOnFront()
    {
        long now = Environment.TickCount64;
        if (now - _lastFrontCheck < 100)
        {
            return _cachedOnFront;
        }

        _lastFrontCheck = now;
        var ignore = GetChromeWindows?.Invoke() ?? [];
        bool raw = OneCWindowFinder.IsOneCOnFront(
            _settings.IncludeThickClient,
            NativeMethods.GetCurrentProcessId(),
            ignore);
        if (raw)
        {
            _frontOffStreak = 0;
            _cachedOnFront = true;
        }
        else
        {
            _frontOffStreak++;
            if (_frontOffStreak >= 4)
            {
                _cachedOnFront = false;
            }
        }

        return _cachedOnFront;
    }

    private void SyncFontSmoothing(bool filterOn)
    {
        FontSmoothingService.Sync(_settings.SoftenOneCText, filterOn, _settings.IncludeThickClient);
    }

    private void ApplyMonitor(MagColorEffect matrix, List<IntPtr> exclude)
    {
        var monitor = MonitorCatalog.FindByDevice(_settings.MonitorDeviceName);
        if (monitor is null)
        {
            HideOverlays();
            SetStatus("Монитор не найден.");
            return;
        }

        _monitorOverlay ??= new MagnifierHost(topmost: true);
        _monitorOverlay.Apply(monitor.Bounds, matrix, NativeConstants.HwndTopmost, IntPtr.Zero, exclude);
        HideOneCOverlays(keep: null);
        SetStatus("Фильтр на мониторе: " + monitor.Title);
    }

    private void ApplyOneC(MagColorEffect matrix, List<IntPtr> exclude)
    {
        var windows = GetOneCWindows();
        if (windows.Count == 0)
        {
            HideOverlays();
            SetStatus("1С не найдена. Запустите тонкий клиент 1cv8c.exe.");
            return;
        }

        _monitorOverlay?.Hide();

        var seen = new HashSet<IntPtr>();
        foreach (var window in windows)
        {
            seen.Add(window.Handle);
            _overlayLostAt.Remove(window.Handle);
            if (!_overlays.TryGetValue(window.Handle, out var overlay))
            {
                overlay = new MagnifierHost(topmost: false);
                _overlays[window.Handle] = overlay;
            }

            IntPtr above = NativeMethods.GetWindow(window.Handle, NativeConstants.GwHwndPrev);
            if (above == overlay.HostHandle)
            {
                above = NativeMethods.GetWindow(above, NativeConstants.GwHwndPrev);
            }

            overlay.Apply(window.Bounds, matrix, above, window.Handle, exclude);
        }

        HideOneCOverlays(seen);

        string sample = windows[0].Title;
        if (string.IsNullOrWhiteSpace(sample))
        {
            sample = "1С:Предприятие";
        }

        SetStatus(windows.Count == 1
            ? "Фильтр на окне 1С: " + TrimTitle(sample)
            : $"Фильтр на {windows.Count} окнах 1С.");
    }

    private List<IntPtr> BuildExcludeList()
    {
        var list = new List<IntPtr>();
        if (GetChromeWindows is not null)
        {
            list.AddRange(GetChromeWindows());
        }

        if (_monitorOverlay is not null)
        {
            list.Add(_monitorOverlay.HostHandle);
            list.Add(_monitorOverlay.MagnifierHandle);
        }

        foreach (var overlay in _overlays.Values)
        {
            list.Add(overlay.HostHandle);
            list.Add(overlay.MagnifierHandle);
        }

        return list.Where(h => h != IntPtr.Zero).Distinct().ToList();
    }

    private void RaiseChrome(IReadOnlyList<IntPtr> chrome)
    {
        if (_settings.Mode != FilterMode.Monitor && _settings.Mode != FilterMode.Desktop)
        {
            return;
        }

        foreach (var hwnd in chrome)
        {
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            {
                continue;
            }

            NativeMethods.SetWindowPos(
                hwnd,
                NativeConstants.HwndTopmost,
                0,
                0,
                0,
                0,
                NativeConstants.SwpNoMove | NativeConstants.SwpNoSize | NativeConstants.SwpNoActivate);
        }
    }

    private void HideOverlays()
    {
        _monitorOverlay?.Hide();
        foreach (var overlay in _overlays.Values)
        {
            overlay.Hide();
        }
    }

    private void HideOneCOverlays(HashSet<IntPtr>? keep)
    {
        long now = Environment.TickCount64;
        var stale = new List<IntPtr>();
        foreach (var key in _overlays.Keys)
        {
            if (keep is null)
            {
                stale.Add(key);
                continue;
            }

            if (keep.Contains(key))
            {
                _overlayLostAt.Remove(key);
                continue;
            }

            _overlays[key].Hide();
            if (!_overlayLostAt.ContainsKey(key))
            {
                _overlayLostAt[key] = now;
            }

            if (now - _overlayLostAt[key] >= 1500)
            {
                stale.Add(key);
            }
        }

        foreach (var key in stale)
        {
            _overlays[key].Dispose();
            _overlays.Remove(key);
            _overlayLostAt.Remove(key);
        }
    }

    private void ClearFullscreen()
    {
        if (!_fullscreenActive)
        {
            return;
        }

        NativeMethods.MagSetFullscreenColorEffect(ref _identity);
        _fullscreenActive = false;
        _fullscreenEffectReady = false;
    }

    private void ResetNow()
    {
        ClearFullscreen();
        HideOverlays();
        HideOneCOverlays(keep: null);
        _monitorOverlay?.Dispose();
        _monitorOverlay = null;
    }

    private void SetStatus(string text)
    {
        if (_status == text)
        {
            return;
        }

        _status = text;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string TrimTitle(string title)
    {
        return title.Length <= 64 ? title : title[..61] + "...";
    }
}
