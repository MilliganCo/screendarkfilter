using ScreenDarkFilter.Native;

namespace ScreenDarkFilter.Overlay;

/// <summary>
/// Следит за перерисовкой и z-order 1С, чтобы фильтр не ждал таймер 50–120 мс.
/// </summary>
internal static class OneCUiWatcher
{
    private static readonly NativeMethods.WinEventProc Proc = OnEvent;
    private static readonly object Gate = new();
    private static HashSet<uint> _pids = [];
    private static IntPtr _hook;
    private static Action<bool>? _raised;

    public static long LastEventTick { get; private set; }

    public static void Start(Action<bool> onRaised)
    {
        _raised = onRaised;
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _hook = NativeMethods.SetWinEventHook(
            NativeConstants.EventMin,
            NativeConstants.EventMax,
            IntPtr.Zero,
            Proc,
            0,
            0,
            NativeConstants.WineventOutOfContext | NativeConstants.WineventSkipOwnProcess);
    }

    public static void Stop()
    {
        _raised = null;
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        _ = NativeMethods.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }

    public static void SetPids(IEnumerable<uint> pids)
    {
        lock (Gate)
        {
            _pids = pids.ToHashSet();
        }
    }

    private static void OnEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint dwmsEventTime)
    {
        if (eventType is NativeConstants.EventObjectLocationChange
            or NativeConstants.EventObjectCreate
            or NativeConstants.EventObjectDestroy)
        {
            return;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0)
        {
            return;
        }

        lock (Gate)
        {
            if (_pids.Count > 0 && !_pids.Contains(pid))
            {
                return;
            }
        }

        LastEventTick = Environment.TickCount64;
        bool zOrder = eventType is NativeConstants.EventSystemForeground
            or NativeConstants.EventObjectReorder
            or NativeConstants.EventObjectFocus
            or NativeConstants.EventObjectShow
            or NativeConstants.EventSystemMenuStart
            or NativeConstants.EventSystemMenuPopupStart;
        _raised?.Invoke(zOrder);
    }
}
