using System.Runtime.InteropServices;

namespace ScreenDarkFilter.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public bool EqualsRect(in RECT other) =>
        Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

    public bool NearlyEquals(in RECT other, int slop = 2) =>
        Math.Abs(Left - other.Left) <= slop
        && Math.Abs(Top - other.Top) <= slop
        && Math.Abs(Right - other.Right) <= slop
        && Math.Abs(Bottom - other.Bottom) <= slop;
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MagColorEffect
{
    public float M00, M01, M02, M03, M04;
    public float M10, M11, M12, M13, M14;
    public float M20, M21, M22, M23, M24;
    public float M30, M31, M32, M33, M34;
    public float M40, M41, M42, M43, M44;

    public static MagColorEffect FromRowMajor(float[] m)
    {
        if (m.Length != 25)
        {
            throw new ArgumentException("Нужна матрица 5x5.", nameof(m));
        }

        return new MagColorEffect
        {
            M00 = m[0],  M01 = m[1],  M02 = m[2],  M03 = m[3],  M04 = m[4],
            M10 = m[5],  M11 = m[6],  M12 = m[7],  M13 = m[8],  M14 = m[9],
            M20 = m[10], M21 = m[11], M22 = m[12], M23 = m[13], M24 = m[14],
            M30 = m[15], M31 = m[16], M32 = m[17], M33 = m[18], M34 = m[19],
            M40 = m[20], M41 = m[21], M42 = m[22], M43 = m[23], M44 = m[24]
        };
    }

    public bool EqualsEffect(in MagColorEffect other) =>
        M00 == other.M00 && M01 == other.M01 && M02 == other.M02 && M03 == other.M03 && M04 == other.M04
        && M10 == other.M10 && M11 == other.M11 && M12 == other.M12 && M13 == other.M13 && M14 == other.M14
        && M20 == other.M20 && M21 == other.M21 && M22 == other.M22 && M23 == other.M23 && M24 == other.M24
        && M30 == other.M30 && M31 == other.M31 && M32 == other.M32 && M33 == other.M33 && M34 == other.M34
        && M40 == other.M40 && M41 == other.M41 && M42 == other.M42 && M43 == other.M43 && M44 == other.M44;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MagTransform
{
    public float M00, M01, M02;
    public float M10, M11, M12;
    public float M20, M21, M22;

    public static MagTransform Identity { get; } = new()
    {
        M00 = 1f, M11 = 1f, M22 = 1f
    };
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WNDCLASSEX
{
    public int CbSize;
    public int Style;
    public IntPtr LpfnWndProc;
    public int CbClsExtra;
    public int CbWndExtra;
    public IntPtr HInstance;
    public IntPtr HIcon;
    public IntPtr HCursor;
    public IntPtr HbrBackground;
    public string? LpszMenuName;
    public string LpszClassName;
    public IntPtr HIconSm;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct MONITORINFOEX
{
    public int CbSize;
    public RECT RcMonitor;
    public RECT RcWork;
    public int DwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string SzDevice;
}

internal static partial class NativeConstants
{
    public const string MagnifierClass = "Magnifier";
    public const string HostClassName = "ScreenDarkFilterMagHost";

    public const int WsPopup = unchecked((int)0x80000000);
    public const int WsChild = 0x40000000;
    public const int WsVisible = 0x10000000;
    public const int WsClipChildren = 0x02000000;
    public const int WsClipSiblings = 0x04000000;

    public const int WsExTopmost = 0x00000008;
    public const int WsExTransparent = 0x00000020;
    public const int WsExToolWindow = 0x00000080;
    public const int WsExLayered = 0x00080000;
    public const int WsExNoActivate = 0x08000000;

    public const int SwpNoSize = 0x0001;
    public const int SwpNoMove = 0x0002;
    public const int SwpNoZOrder = 0x0004;
    public const int SwpNoActivate = 0x0010;
    public const int SwpNoRedraw = 0x0008;
    public const int SwpShowWindow = 0x0040;
    public const int SwpHideWindow = 0x0080;

    public const int SwHide = 0;
    public const int SwShowNoActivate = 8;

    public const int LwaAlpha = 0x02;
    public const int MwFilterModeExclude = 0;
    public const int GwlExStyle = -20;
    public const int GwHwndPrev = 3;
    public const int GwOwner = 4;
    public const int DwmwaExtendedFrameBounds = 9;
    public const int DwmwaCloaked = 14;
    public const int MonitorDefaultToNearest = 2;
    public const int MonitorInfoPrimary = 1;
    public const uint ProcessQueryLimitedInformation = 0x1000;
    public const int ErrorClassAlreadyExists = 1410;
    public const int HotkeyId = 0x5DF1;
    public const int WmHotkey = 0x0312;
    public const int WmTimer = 0x0113;
    public const int WmEraseBkgnd = 0x0014;
    public const int NullBrush = 5;
    public static readonly IntPtr MagRefreshTimerId = new(1);
    public const uint WineventOutOfContext = 0;
    public const uint WineventSkipOwnProcess = 2;
    public const uint EventMin = 0x0001;
    public const uint EventMax = 0x7FFFFFFF;
    public const uint EventSystemForeground = 0x0003;
    public const uint EventSystemMenuStart = 0x0004;
    public const uint EventSystemMenuPopupStart = 0x0006;
    public const uint EventObjectCreate = 0x8000;
    public const uint EventObjectDestroy = 0x8001;
    public const uint EventObjectShow = 0x8002;
    public const uint EventObjectFocus = 0x8005;
    public const uint EventObjectReorder = 0x8004;
    public const uint EventObjectLocationChange = 0x800B;
    public const int ModAlt = 0x0001;
    public const int ModControl = 0x0002;
    public const int ModNoRepeat = 0x4000;
    public const int VkD = 0x44;

    public const uint SpiGetFontSmoothing = 0x004A;
    public const uint SpiSetFontSmoothing = 0x004B;
    public const uint SpiGetFontSmoothingType = 0x200A;
    public const uint SpiSetFontSmoothingType = 0x200B;
    public const uint SpiGetFontSmoothingContrast = 0x200C;
    public const uint SpiSetFontSmoothingContrast = 0x200D;
    public const uint FontSmoothingStandard = 1;
    public const uint FontSmoothingClearType = 2;
    public const uint SpifSendChange = 0x0002;
    public const uint RdwInvalidate = 0x0001;
    public const uint RdwErase = 0x0004;
    public const uint RdwAllChildren = 0x0080;
    public const uint RdwFrame = 0x0400;

    public static readonly IntPtr HwndTop = IntPtr.Zero;
    public static readonly IntPtr HwndTopmost = new(-1);
    public static readonly IntPtr HwndMessage = new(-3);
}
