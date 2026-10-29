using System.Runtime.InteropServices;
using ScreenDarkFilter.Native;

namespace ScreenDarkFilter.Overlay;

internal sealed record MonitorTarget(IntPtr Handle, string DeviceName, string Title, RECT Bounds, bool IsPrimary);

internal static class MonitorCatalog
{
    private static readonly NativeMethods.MonitorEnumProc EnumProc = EnumCallback;
    private static List<MonitorTarget>? _current;
    private static int _index;

    public static IReadOnlyList<MonitorTarget> Enumerate()
    {
        _current = [];
        _index = 0;
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumProc, IntPtr.Zero);
        return _current;
    }

    public static MonitorTarget? FindByDevice(string? deviceName)
    {
        var all = Enumerate();
        if (all.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            var match = all.FirstOrDefault(m =>
                string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return all.FirstOrDefault(m => m.IsPrimary) ?? all[0];
    }

    private static bool EnumCallback(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData)
    {
        var info = new MONITORINFOEX
        {
            CbSize = Marshal.SizeOf<MONITORINFOEX>()
        };

        if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
        {
            return true;
        }

        _index++;
        bool primary = (info.DwFlags & NativeConstants.MonitorInfoPrimary) != 0;
        string device = string.IsNullOrWhiteSpace(info.SzDevice) ? $"DISPLAY{_index}" : info.SzDevice;
        string title = primary
            ? $"Монитор {_index} (основной) {info.RcMonitor.Width}×{info.RcMonitor.Height}"
            : $"Монитор {_index} {info.RcMonitor.Width}×{info.RcMonitor.Height}";

        _current!.Add(new MonitorTarget(hMonitor, device, title, info.RcMonitor, primary));
        return true;
    }
}
