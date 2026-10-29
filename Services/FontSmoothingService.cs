using ScreenDarkFilter.Native;
using ScreenDarkFilter.Overlay;

namespace ScreenDarkFilter.Services;

/// <summary>
/// Пока фильтр включён, временно переключает сглаживание с ClearType на обычное grayscale.
/// Не пишет значение в user ini: при выключении и выходе возвращает как было.
/// </summary>
internal static class FontSmoothingService
{
    private static readonly object Gate = new();
    private static bool _captured;
    private static bool _applied;
    private static uint _origEnabled;
    private static uint _origType;
    private static uint _origContrast;

    public static void Sync(bool softenRequested, bool filterOn, bool includeThickClient)
    {
        bool want = softenRequested && filterOn;
        lock (Gate)
        {
            if (want == _applied)
            {
                return;
            }

            if (want)
            {
                Apply();
            }
            else
            {
                RestoreUnlocked();
            }
        }

        OneCWindowFinder.Redraw(includeThickClient);
    }

    public static void Restore()
    {
        lock (Gate)
        {
            RestoreUnlocked();
        }
    }

    private static void Apply()
    {
        CaptureIfNeeded();

        uint enabled = 1;
        _ = NativeMethods.SystemParametersInfo(
            NativeConstants.SpiSetFontSmoothing,
            enabled,
            IntPtr.Zero,
            NativeConstants.SpifSendChange);

        uint type = NativeConstants.FontSmoothingStandard;
        _ = NativeMethods.SystemParametersInfo(
            NativeConstants.SpiSetFontSmoothingType,
            0,
            ref type,
            NativeConstants.SpifSendChange);

        _applied = true;
    }

    private static void RestoreUnlocked()
    {
        if (!_applied && !_captured)
        {
            return;
        }

        if (_captured)
        {
            _ = NativeMethods.SystemParametersInfo(
                NativeConstants.SpiSetFontSmoothing,
                _origEnabled,
                IntPtr.Zero,
                NativeConstants.SpifSendChange);

            uint type = _origType == 0 ? NativeConstants.FontSmoothingClearType : _origType;
            _ = NativeMethods.SystemParametersInfo(
                NativeConstants.SpiSetFontSmoothingType,
                0,
                ref type,
                NativeConstants.SpifSendChange);

            if (_origContrast != 0)
            {
                uint contrast = _origContrast;
                _ = NativeMethods.SystemParametersInfo(
                    NativeConstants.SpiSetFontSmoothingContrast,
                    0,
                    ref contrast,
                    NativeConstants.SpifSendChange);
            }
        }

        _applied = false;
    }

    private static void CaptureIfNeeded()
    {
        if (_captured)
        {
            return;
        }

        uint enabled = 0;
        _ = NativeMethods.SystemParametersInfo(
            NativeConstants.SpiGetFontSmoothing,
            0,
            ref enabled,
            0);
        _origEnabled = enabled;

        uint type = 0;
        _ = NativeMethods.SystemParametersInfo(
            NativeConstants.SpiGetFontSmoothingType,
            0,
            ref type,
            0);
        _origType = type;

        uint contrast = 0;
        _ = NativeMethods.SystemParametersInfo(
            NativeConstants.SpiGetFontSmoothingContrast,
            0,
            ref contrast,
            0);
        _origContrast = contrast;
        _captured = true;
    }
}
