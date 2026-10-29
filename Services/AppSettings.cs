using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenDarkFilter.Services;

internal enum FilterMode
{
    Desktop,
    Monitor,
    OneC
}

internal sealed class AppSettings : INotifyPropertyChanged
{
    private bool _enabled = true;
    private FilterMode _mode = FilterMode.OneC;
    private string _themeId = "dark1c";
    private string _monitorDeviceName = "";
    private int _background = 16;
    private int _text = 82;
    private int _contrast = 52;
    private int _saturation = 38;
    private int _strength = 100;
    private int _warmth = 50;
    private int _midtones = 50;
    private bool _onlyWhenOneCActive;
    private bool _includeThickClient = true;
    private bool _softenOneCText;
    private bool _startWithWindows;
    private bool _closeToTray = true;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Changed;

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public FilterMode Mode
    {
        get => _mode;
        set => Set(ref _mode, value);
    }

    public string ThemeId
    {
        get => _themeId;
        set => Set(ref _themeId, value);
    }

    public string MonitorDeviceName
    {
        get => _monitorDeviceName;
        set => Set(ref _monitorDeviceName, value ?? "");
    }

    public int Background
    {
        get => _background;
        set => Set(ref _background, Math.Clamp(value, 0, 100));
    }

    public int Text
    {
        get => _text;
        set => Set(ref _text, Math.Clamp(value, 0, 100));
    }

    public int Contrast
    {
        get => _contrast;
        set => Set(ref _contrast, Math.Clamp(value, 0, 100));
    }

    public int Saturation
    {
        get => _saturation;
        set => Set(ref _saturation, Math.Clamp(value, 0, 100));
    }

    public int Strength
    {
        get => _strength;
        set => Set(ref _strength, Math.Clamp(value, 0, 100));
    }

    public int Warmth
    {
        get => _warmth;
        set => Set(ref _warmth, Math.Clamp(value, 0, 100));
    }

    public int Midtones
    {
        get => _midtones;
        set => Set(ref _midtones, Math.Clamp(value, 0, 100));
    }

    public bool OnlyWhenOneCActive
    {
        get => _onlyWhenOneCActive;
        set => Set(ref _onlyWhenOneCActive, value);
    }

    public bool IncludeThickClient
    {
        get => _includeThickClient;
        set => Set(ref _includeThickClient, value);
    }

    public bool SoftenOneCText
    {
        get => _softenOneCText;
        set => Set(ref _softenOneCText, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => Set(ref _startWithWindows, value);
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set => Set(ref _closeToTray, value);
    }

    public void CopyFrom(AppSettings other)
    {
        Enabled = other.Enabled;
        Mode = other.Mode;
        ThemeId = other.ThemeId;
        MonitorDeviceName = other.MonitorDeviceName;
        Background = other.Background;
        Text = other.Text;
        Contrast = other.Contrast;
        Saturation = other.Saturation;
        Strength = other.Strength;
        Warmth = other.Warmth;
        Midtones = other.Midtones;
        OnlyWhenOneCActive = other.OnlyWhenOneCActive;
        IncludeThickClient = other.IncludeThickClient;
        SoftenOneCText = other.SoftenOneCText;
        StartWithWindows = other.StartWithWindows;
        CloseToTray = other.CloseToTray;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
