using System.Windows;
using System.Windows.Interop;
using ScreenDarkFilter.Native;
using ScreenDarkFilter.Services;
using ColorMatrix5 = ScreenDarkFilter.Color.ColorMatrix5;

namespace ScreenDarkFilter;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private AppSettings? _settings;
    private FilterService? _filter;
    private TrayService? _tray;
    private HotkeyService? _hotkey;
    private MainWindow? _main;
    private bool _magReady;
    private CancellationTokenSource? _showCts;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args.Select(a => a.Trim()).ToArray();
        bool resetOnly = args.Any(a => a.Equals("--reset", StringComparison.OrdinalIgnoreCase));
        bool autostart = args.Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));

        if (resetOnly)
        {
            FilterService.EmergencyReset();
            NativeMethods.MagUninitialize();
            Shutdown();
            return;
        }

        _mutex = new Mutex(true, @"Local\ScreenDarkFilter.SingleInstance.v1", out bool created);
        if (!created)
        {
            try
            {
                EventWaitHandle.OpenExisting(@"Local\ScreenDarkFilter.ShowWindow").Set();
            }
            catch
            {
                // первое окно могло уже закрыться
            }

            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ScreenDarkFilter.ShowWindow");
        _showCts = new CancellationTokenSource();
        _ = Task.Run(() => WaitForShow(_showCts.Token));

        if (!Environment.Is64BitProcess)
        {
            System.Windows.MessageBox.Show(
                "Нужна 64-битная сборка. Magnification API не работает через WOW64.",
                "ScreenDarkFilter",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (!NativeMethods.MagInitialize())
        {
            System.Windows.MessageBox.Show(
                "Не удалось запустить Magnification API.\nЗакройте штатную экранную лупу Windows и соберите проект как x64.",
                "ScreenDarkFilter",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        _magReady = true;
        DispatcherUnhandledException += (_, argsEx) =>
        {
            FilterService.EmergencyReset();
            argsEx.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, _) => FilterService.EmergencyReset();
        Exit += (_, _) => Cleanup();

        _settings = SettingsStore.Load();
        StartupService.Apply(_settings.StartWithWindows);
        _filter = new FilterService(_settings)
        {
            GetChromeWindows = GetChromeWindows
        };
        _main = new MainWindow(_settings, _filter);
        _tray = new TrayService(_settings, _filter, ShowMain, ExitApp);
        _hotkey = new HotkeyService(_filter);
        _filter.Start();

        if (!autostart)
        {
            ShowMain();
        }
    }

    private IReadOnlyList<IntPtr> GetChromeWindows()
    {
        var list = new List<IntPtr>();
        if (_main is not null && _main.IsVisible)
        {
            IntPtr hwnd = new WindowInteropHelper(_main).Handle;
            if (hwnd != IntPtr.Zero)
            {
                list.Add(hwnd);
            }
        }

        return list;
    }

    private void WaitForShow(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_showEvent is not null && _showEvent.WaitOne(500))
                {
                    Dispatcher.Invoke(ShowMain);
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private void ShowMain()
    {
        if (_main is null)
        {
            return;
        }

        if (!_main.IsVisible)
        {
            _main.Show();
        }

        _main.ShowInTaskbar = true;
        if (_main.WindowState == WindowState.Minimized)
        {
            _main.WindowState = WindowState.Normal;
        }

        _main.Activate();
    }

    private void ExitApp()
    {
        _settings!.CloseToTray = false;
        Shutdown();
    }

    private void Cleanup()
    {
        _showCts?.Cancel();
        _hotkey?.Dispose();
        _tray?.Dispose();
        _filter?.Dispose();
        FontSmoothingService.Restore();
        if (_settings is not null)
        {
            SettingsStore.Save(_settings);
        }

        if (_magReady)
        {
            var identity = ColorMatrix5.Identity().ToNative();
            NativeMethods.MagSetFullscreenColorEffect(ref identity);
            NativeMethods.MagUninitialize();
            _magReady = false;
        }

        _showEvent?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}
