using System.IO;
using System.Text;
using ScreenDarkFilter.Native;

namespace ScreenDarkFilter.Overlay;

internal sealed record OneCWindow(IntPtr Handle, RECT Bounds, string Title);

internal static class OneCWindowFinder
{
    private static readonly NativeMethods.EnumWindowsProc EnumProc = EnumCallback;
    private static readonly NativeMethods.EnumWindowsProc FrontProc = FrontCallback;
    private static readonly Dictionary<uint, (string Name, long Tick)> ProcessNameCache = [];
    private static readonly HashSet<string> SkipClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "IME",
        "MSCTFIME UI",
        "tooltips_class32",
        "SysShadow",
        "OleMainThreadWndClass"
    };

    private static readonly HashSet<string> SkipFrontClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "IME",
        "MSCTFIME UI",
        "tooltips_class32",
        "SysShadow",
        "OleMainThreadWndClass",
        NativeConstants.MagnifierClass,
        NativeConstants.HostClassName,
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "Progman",
        "WorkerW",
        "DummyDWMListenerWindow",
        "Windows.Internal.Shell.TabProxyWindow",
        "EdgeUiInputTopWndClass",
        "ApplicationManager_ImmersiveShellWindow"
    };

    private static List<OneCWindow>? _current;
    private static FrontScan? _front;

    public static IReadOnlyList<OneCWindow> Find(bool includeThickClient)
    {
        _current = [];
        NativeMethods.EnumWindows(EnumProc, includeThickClient ? 1 : 0);
        return _current;
    }

    public static bool IsOneCForeground(bool includeThickClient)
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        return IsOneCProcess(pid, includeThickClient);
    }

    /// <summary>
    /// 1С считается на переднем плане, если её окно видно и не закрыто чужим окном,
    /// даже когда фокус у настроек ScreenDarkFilter или у окна на другом мониторе.
    /// </summary>
    public static bool IsOneCOnFront(
        bool includeThickClient,
        uint ignorePid,
        IEnumerable<IntPtr>? ignoreHwnds)
    {
        _front = new FrontScan
        {
            IncludeThick = includeThickClient,
            IgnorePid = ignorePid,
            IgnoreHwnds = ignoreHwnds is null
                ? []
                : ignoreHwnds.Where(h => h != IntPtr.Zero).ToHashSet()
        };

        NativeMethods.EnumWindows(FrontProc, IntPtr.Zero);
        return _front.FoundOnFront;
    }

    public static void Redraw(bool includeThickClient)
    {
        const uint flags = NativeConstants.RdwInvalidate
                           | NativeConstants.RdwErase
                           | NativeConstants.RdwAllChildren
                           | NativeConstants.RdwFrame;

        foreach (var window in Find(includeThickClient))
        {
            NativeMethods.RedrawWindow(window.Handle, IntPtr.Zero, IntPtr.Zero, flags);
            NativeMethods.InvalidateRect(window.Handle, IntPtr.Zero, true);
        }
    }

    private static bool EnumCallback(IntPtr hwnd, IntPtr lParam)
    {
        bool includeThick = lParam != IntPtr.Zero;
        if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd) || NativeMethods.IsCloaked(hwnd))
        {
            return true;
        }

        string className = NativeMethods.GetWindowClass(hwnd);
        if (SkipClasses.Contains(className))
        {
            return true;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (!IsOneCProcess(pid, includeThick))
        {
            return true;
        }

        if (!TryAcceptOneCBounds(hwnd, out RECT bounds))
        {
            return true;
        }

        _current!.Add(new OneCWindow(hwnd, bounds, NativeMethods.GetWindowTitle(hwnd)));
        return true;
    }

    private static bool FrontCallback(IntPtr hwnd, IntPtr lParam)
    {
        var scan = _front!;
        if (scan.FoundOnFront)
        {
            return false;
        }

        if (scan.IgnoreHwnds.Contains(hwnd))
        {
            return true;
        }

        if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd) || NativeMethods.IsCloaked(hwnd))
        {
            return true;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == scan.IgnorePid)
        {
            return true;
        }

        string className = NativeMethods.GetWindowClass(hwnd);
        if (SkipFrontClasses.Contains(className))
        {
            return true;
        }

        int ex = NativeMethods.GetWindowLong(hwnd, NativeConstants.GwlExStyle);
        if ((ex & NativeConstants.WsExTransparent) != 0)
        {
            return true;
        }

        if (!NativeMethods.TryGetVisibleBounds(hwnd, out RECT bounds) || bounds.Width < 80 || bounds.Height < 50)
        {
            return true;
        }

        if (IsOneCProcess(pid, scan.IncludeThick))
        {
            if (!TryAcceptOneCBounds(hwnd, out bounds))
            {
                return true;
            }

            bool covered = scan.Occluders.Any(o => Covers(o, bounds));
            if (!covered)
            {
                scan.FoundOnFront = true;
                return false;
            }

            return true;
        }

        if (IsSubstantialOccluder(bounds, ex))
        {
            scan.Occluders.Add(bounds);
        }

        return true;
    }

    private static bool TryAcceptOneCBounds(IntPtr hwnd, out RECT bounds)
    {
        if (!NativeMethods.TryGetVisibleBounds(hwnd, out bounds))
        {
            return false;
        }

        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeConstants.GwlExStyle);
        bool toolWindow = (exStyle & NativeConstants.WsExToolWindow) != 0;
        if (toolWindow && (bounds.Width < 220 || bounds.Height < 80))
        {
            return false;
        }

        return bounds.Width >= 120 && bounds.Height >= 80;
    }

    private static bool IsSubstantialOccluder(in RECT bounds, int exStyle)
    {
        if ((exStyle & NativeConstants.WsExToolWindow) != 0 && (bounds.Width < 280 || bounds.Height < 120))
        {
            return false;
        }

        if ((exStyle & NativeConstants.WsExNoActivate) != 0 && bounds.Width < 400)
        {
            return false;
        }

        return bounds.Width >= 180 && bounds.Height >= 90;
    }

    /// <summary>
    /// Чужое окно «закрывает» 1С, если перекрывает большую часть её площади.
    /// Маленькое окно настроек поверх 1С фильтр не снимает.
    /// </summary>
    private static bool Covers(in RECT outer, in RECT inner)
    {
        int innerArea = inner.Width * inner.Height;
        if (innerArea <= 0)
        {
            return false;
        }

        int overlap = IntersectionArea(outer, inner);
        return overlap >= innerArea * 0.70;
    }

    private static int IntersectionArea(in RECT a, in RECT b)
    {
        int left = Math.Max(a.Left, b.Left);
        int top = Math.Max(a.Top, b.Top);
        int right = Math.Min(a.Right, b.Right);
        int bottom = Math.Min(a.Bottom, b.Bottom);
        if (right <= left || bottom <= top)
        {
            return 0;
        }

        return (right - left) * (bottom - top);
    }

    private static bool IsOneCProcess(uint pid, bool includeThickClient)
    {
        string? name = GetProcessName(pid);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (name.Equals("1cv8c", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return includeThickClient && name.Equals("1cv8", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetProcessName(uint pid)
    {
        long now = Environment.TickCount64;
        if (ProcessNameCache.TryGetValue(pid, out var cached) && now - cached.Tick < 2000)
        {
            return cached.Name;
        }

        IntPtr handle = NativeMethods.OpenProcess(NativeConstants.ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            int size = buffer.Capacity;
            if (!NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size))
            {
                return null;
            }

            string name = Path.GetFileNameWithoutExtension(buffer.ToString());
            ProcessNameCache[pid] = (name, now);
            return name;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    private sealed class FrontScan
    {
        public bool IncludeThick;
        public uint IgnorePid;
        public HashSet<IntPtr> IgnoreHwnds = [];
        public List<RECT> Occluders = [];
        public bool FoundOnFront;
    }
}
