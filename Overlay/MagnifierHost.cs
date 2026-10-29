using System.ComponentModel;
using System.Runtime.InteropServices;
using ScreenDarkFilter.Native;

namespace ScreenDarkFilter.Overlay;

internal sealed class MagnifierHost : IDisposable
{
    private static readonly NativeMethods.WndProc HostWndProcDelegate = HostWndProc;
    private static readonly object RegisterLock = new();
    private static readonly Dictionary<IntPtr, MagnifierHost> Hosts = [];
    private static bool _classRegistered;

    private readonly bool _topmost;
    private IntPtr _host;
    private IntPtr _magnifier;
    private RECT _lastBounds;
    private IntPtr _lastInsertAfter = new(-2);
    private MagColorEffect _lastEffect;
    private IntPtr[] _lastExclude = [];
    private bool _hidden = true;
    private bool _sourceReady;
    private bool _transformReady;
    private bool _effectReady;
    private bool _filterReady;
    private long _lastZOrderTick;
    private long _lastPresentTick;
    private long _lastPresentedEventTick;
    private long _suppressPresentUntil;

    public IntPtr HostHandle => _host;
    public IntPtr MagnifierHandle => _magnifier;

    public MagnifierHost(bool topmost)
    {
        _topmost = topmost;
        EnsureClassRegistered();
        CreateWindows();
    }

    public void Apply(in RECT bounds, MagColorEffect effect, IntPtr insertAfter, IntPtr targetHwnd, IReadOnlyList<IntPtr> exclude)
    {
        if (_host == IntPtr.Zero || _magnifier == IntPtr.Zero)
        {
            return;
        }

        if (bounds.Width <= 2 || bounds.Height <= 2)
        {
            Hide();
            return;
        }

        bool wasHidden = _hidden;
        bool boundsChanged = !_sourceReady || !bounds.NearlyEquals(_lastBounds);
        bool effectChanged = !_effectReady || !effect.EqualsEffect(_lastEffect);
        var excludeArray = exclude
            .Concat([_host, _magnifier])
            .Where(h => h != IntPtr.Zero)
            .Distinct()
            .OrderBy(h => h.ToInt64())
            .ToArray();
        bool excludeChanged = !_filterReady || !excludeArray.SequenceEqual(_lastExclude);

        long now = Environment.TickCount64;
        // Достаточно, чтобы лупа была где-то выше 1С. Меню/комбо между ними не трогаем.
        bool aboveTarget = _topmost || IsHostAboveTarget(_host, targetHwnd);
        bool allowZ = wasHidden || !aboveTarget;

        if (wasHidden || boundsChanged || allowZ)
        {
            int flags = NativeConstants.SwpNoActivate;
            if (!boundsChanged)
            {
                flags |= NativeConstants.SwpNoMove | NativeConstants.SwpNoSize;
                if (!wasHidden)
                {
                    flags |= NativeConstants.SwpNoRedraw;
                }
            }

            if (!allowZ)
            {
                flags |= NativeConstants.SwpNoZOrder;
            }

            if (wasHidden)
            {
                flags |= NativeConstants.SwpShowWindow;
            }

            IntPtr z = _topmost
                ? NativeConstants.HwndTopmost
                : (insertAfter == IntPtr.Zero ? NativeConstants.HwndTop : insertAfter);
            NativeMethods.SetWindowPos(_host, z, bounds.Left, bounds.Top, bounds.Width, bounds.Height, flags);
            if (allowZ)
            {
                _lastInsertAfter = insertAfter;
                _lastZOrderTick = now;
                if (!wasHidden && !boundsChanged)
                {
                    _suppressPresentUntil = now + 40;
                }
            }
        }

        if (boundsChanged)
        {
            NativeMethods.GetClientRect(_host, out RECT client);
            NativeMethods.SetWindowPos(
                _magnifier,
                IntPtr.Zero,
                0,
                0,
                Math.Max(client.Width, 1),
                Math.Max(client.Height, 1),
                NativeConstants.SwpNoZOrder | NativeConstants.SwpNoActivate);
            _lastBounds = bounds;
        }

        if (excludeChanged)
        {
            _ = NativeMethods.MagSetWindowFilterList(
                _magnifier,
                NativeConstants.MwFilterModeExclude,
                excludeArray.Length,
                excludeArray);
            _lastExclude = excludeArray;
            _filterReady = true;
        }

        if (effectChanged)
        {
            _ = NativeMethods.MagSetColorEffect(_magnifier, ref effect);
            _lastEffect = effect;
            _effectReady = true;
        }

        if (!_transformReady)
        {
            var transform = MagTransform.Identity;
            _ = NativeMethods.MagSetWindowTransform(_magnifier, ref transform);
            _transformReady = true;
        }

        // Первый кадр сразу. Дальше картинку гоняет WM_TIMER — без SetWindowPos.
        if (boundsChanged || wasHidden || !_sourceReady)
        {
            Present(bounds);
        }

        _hidden = false;
    }

    private void Present(in RECT bounds)
    {
        if (_magnifier == IntPtr.Zero)
        {
            return;
        }

        _ = NativeMethods.MagSetWindowSource(_magnifier, bounds);
        _lastBounds = bounds;
        _sourceReady = true;
        _lastPresentTick = Environment.TickCount64;
    }

    public void Restack(IntPtr insertAfter, IntPtr targetHwnd)
    {
        if (_hidden || _topmost || _host == IntPtr.Zero)
        {
            return;
        }

        if (IsHostAboveTarget(_host, targetHwnd))
        {
            return;
        }

        IntPtr z = insertAfter == IntPtr.Zero ? NativeConstants.HwndTop : insertAfter;
        NativeMethods.SetWindowPos(
            _host,
            z,
            0,
            0,
            0,
            0,
            NativeConstants.SwpNoActivate
            | NativeConstants.SwpNoMove
            | NativeConstants.SwpNoSize
            | NativeConstants.SwpNoRedraw);
        _lastZOrderTick = Environment.TickCount64;
        _suppressPresentUntil = _lastZOrderTick + 40;
    }

    private void OnRefreshTimer()
    {
        if (_hidden || !_sourceReady)
        {
            return;
        }

        long now = Environment.TickCount64;
        if (now < _suppressPresentUntil)
        {
            return;
        }

        long ui = OneCUiWatcher.LastEventTick;
        bool uiDue = ui > _lastPresentedEventTick && now - _lastPresentTick >= 30;
        bool fallbackDue = now - _lastPresentTick >= 200;
        if (!uiDue && !fallbackDue)
        {
            return;
        }

        if (uiDue)
        {
            _lastPresentedEventTick = ui;
        }

        _lastPresentTick = now;
        _ = NativeMethods.MagSetWindowSource(_magnifier, _lastBounds);
    }

    private static bool IsHostAboveTarget(IntPtr host, IntPtr target)
    {
        if (host == IntPtr.Zero || target == IntPtr.Zero)
        {
            return false;
        }

        IntPtr hwnd = NativeMethods.GetWindow(target, NativeConstants.GwHwndPrev);
        for (int i = 0; i < 12 && hwnd != IntPtr.Zero; i++)
        {
            if (hwnd == host)
            {
                return true;
            }

            hwnd = NativeMethods.GetWindow(hwnd, NativeConstants.GwHwndPrev);
        }

        return false;
    }

    public void Hide()
    {
        if (_host == IntPtr.Zero || _hidden)
        {
            return;
        }

        NativeMethods.ShowWindow(_host, NativeConstants.SwHide);
        _hidden = true;
        _sourceReady = false;
        _lastInsertAfter = new(-2);
    }

    public void Dispose()
    {
        if (_host != IntPtr.Zero)
        {
            _ = NativeMethods.KillTimer(_host, NativeConstants.MagRefreshTimerId);
            Hosts.Remove(_host);
        }

        if (_magnifier != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_magnifier);
            _magnifier = IntPtr.Zero;
        }

        if (_host != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_host);
            _host = IntPtr.Zero;
        }
    }

    private void CreateWindows()
    {
        IntPtr instance = NativeMethods.GetModuleHandle(null);
        int exStyle = NativeConstants.WsExLayered
                      | NativeConstants.WsExTransparent
                      | NativeConstants.WsExToolWindow
                      | NativeConstants.WsExNoActivate;
        if (_topmost)
        {
            exStyle |= NativeConstants.WsExTopmost;
        }

        _host = NativeMethods.CreateWindowEx(
            exStyle,
            NativeConstants.HostClassName,
            "ScreenDarkFilter host",
            NativeConstants.WsPopup | NativeConstants.WsClipChildren,
            0,
            0,
            32,
            32,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);

        if (_host == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось создать окно фильтра.");
        }

        NativeMethods.SetLayeredWindowAttributes(_host, 0, 255, NativeConstants.LwaAlpha);
        Hosts[_host] = this;

        _magnifier = NativeMethods.CreateWindowEx(
            0,
            NativeConstants.MagnifierClass,
            "ScreenDarkFilter mag",
            NativeConstants.WsChild | NativeConstants.WsVisible | NativeConstants.WsClipSiblings,
            0,
            0,
            32,
            32,
            _host,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);

        if (_magnifier == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось создать Magnifier. Нужна 64-битная сборка.");
        }

        _ = NativeMethods.SetTimer(_host, NativeConstants.MagRefreshTimerId, 16, IntPtr.Zero);
        Hide();
    }

    private static void EnsureClassRegistered()
    {
        lock (RegisterLock)
        {
            if (_classRegistered)
            {
                return;
            }

            var wndClass = new WNDCLASSEX
            {
                CbSize = Marshal.SizeOf<WNDCLASSEX>(),
                LpfnWndProc = Marshal.GetFunctionPointerForDelegate(HostWndProcDelegate),
                HInstance = NativeMethods.GetModuleHandle(null),
                HbrBackground = NativeMethods.GetStockObject(NativeConstants.NullBrush),
                LpszClassName = NativeConstants.HostClassName
            };

            ushort atom = NativeMethods.RegisterClassEx(ref wndClass);
            if (atom == 0)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != NativeConstants.ErrorClassAlreadyExists)
                {
                    throw new Win32Exception(error, "Не удалось зарегистрировать класс окна фильтра.");
                }
            }

            _classRegistered = true;
        }
    }

    private static IntPtr HostWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeConstants.WmEraseBkgnd)
        {
            return new IntPtr(1);
        }

        if (msg == NativeConstants.WmTimer
            && wParam == NativeConstants.MagRefreshTimerId
            && Hosts.TryGetValue(hWnd, out MagnifierHost? host))
        {
            host.OnRefreshTimer();
            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}
