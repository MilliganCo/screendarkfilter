using System.Windows;
using System.Windows.Interop;
using ScreenDarkFilter.Native;

namespace ScreenDarkFilter.Services;

internal sealed class HotkeyService : IDisposable
{
    private readonly FilterService _filter;
    private HwndSource? _source;
    private bool _registered;

    public HotkeyService(FilterService filter)
    {
        _filter = filter;
        var parameters = new HwndSourceParameters("ScreenDarkFilterHotkeys")
        {
            Width = 0,
            Height = 0,
            ParentWindow = NativeConstants.HwndMessage,
            WindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(Hook);

        _registered = NativeMethods.RegisterHotKey(
            _source.Handle,
            NativeConstants.HotkeyId,
            NativeConstants.ModControl | NativeConstants.ModAlt | NativeConstants.ModNoRepeat,
            NativeConstants.VkD);

        if (!_registered)
        {
            _registered = NativeMethods.RegisterHotKey(
                _source.Handle,
                NativeConstants.HotkeyId,
                NativeConstants.ModControl | NativeConstants.ModAlt,
                NativeConstants.VkD);
        }
    }

    public bool Registered => _registered;

    public void Dispose()
    {
        if (_source is not null && _registered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, NativeConstants.HotkeyId);
        }

        _source?.Dispose();
        _source = null;
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeConstants.WmHotkey && wParam.ToInt32() == NativeConstants.HotkeyId)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _filter.Toggle());
            handled = true;
        }

        return IntPtr.Zero;
    }
}
