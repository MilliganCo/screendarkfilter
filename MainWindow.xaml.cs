using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ScreenDarkFilter.Color;
using ScreenDarkFilter.Overlay;
using ScreenDarkFilter.Services;

namespace ScreenDarkFilter;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly FilterService _filter;
    private bool _syncing;

    internal MainWindow(AppSettings settings, FilterService filter)
    {
        _settings = settings;
        _filter = filter;
        InitializeComponent();

        ThemeBox.ItemsSource = ThemeEngine.Themes;
        ReloadMonitors();
        LoadFromSettings();

        EnabledBox.Click += (_, _) => _settings.Enabled = EnabledBox.IsChecked == true;
        ModeOneC.Checked += (_, _) => _settings.Mode = FilterMode.OneC;
        ModeMonitor.Checked += (_, _) => _settings.Mode = FilterMode.Monitor;
        ModeDesktop.Checked += (_, _) => _settings.Mode = FilterMode.Desktop;
        MonitorBox.SelectionChanged += MonitorBoxOnSelectionChanged;
        ThemeBox.SelectionChanged += ThemeBoxOnSelectionChanged;
        StrengthSlider.ValueChanged += (_, _) => { if (!_syncing) _settings.Strength = (int)StrengthSlider.Value; };
        BackgroundSlider.ValueChanged += (_, _) => { if (!_syncing) _settings.Background = (int)BackgroundSlider.Value; };
        TextSlider.ValueChanged += (_, _) => { if (!_syncing) _settings.Text = (int)TextSlider.Value; };
        ContrastSlider.ValueChanged += (_, _) => { if (!_syncing) _settings.Contrast = (int)ContrastSlider.Value; };
        SaturationSlider.ValueChanged += (_, _) => { if (!_syncing) _settings.Saturation = (int)SaturationSlider.Value; };
        WarmthSlider.ValueChanged += (_, _) => { if (!_syncing) _settings.Warmth = (int)WarmthSlider.Value; };
        MidtonesSlider.ValueChanged += (_, _) => { if (!_syncing) _settings.Midtones = (int)MidtonesSlider.Value; };
        SoftenBox.Click += (_, _) => _settings.SoftenOneCText = SoftenBox.IsChecked == true;
        OnlyOneCBox.Click += (_, _) => _settings.OnlyWhenOneCActive = OnlyOneCBox.IsChecked == true;
        ThickClientBox.Click += (_, _) => _settings.IncludeThickClient = ThickClientBox.IsChecked == true;
        StartupBox.Click += (_, _) => _settings.StartWithWindows = StartupBox.IsChecked == true;
        TrayBox.Click += (_, _) => _settings.CloseToTray = TrayBox.IsChecked == true;
        ResetSlidersButton.Click += (_, _) =>
            ThemeEngine.ApplyDefaults(_settings, ThemeEngine.Resolve(_settings.ThemeId));
        DisableButton.Click += (_, _) => _filter.Disable();

        _settings.Changed += (_, _) => Dispatcher.Invoke(LoadFromSettings);
        _filter.StatusChanged += (_, _) => Dispatcher.Invoke(UpdateStatus);
        SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.Invoke(ReloadMonitors);
        UpdateStatus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
            return;
        }

        System.Windows.Application.Current.Shutdown();
    }

    private void LoadFromSettings()
    {
        _syncing = true;
        EnabledBox.IsChecked = _settings.Enabled;
        ModeOneC.IsChecked = _settings.Mode == FilterMode.OneC;
        ModeMonitor.IsChecked = _settings.Mode == FilterMode.Monitor;
        ModeDesktop.IsChecked = _settings.Mode == FilterMode.Desktop;
        StrengthSlider.Value = _settings.Strength;
        BackgroundSlider.Value = _settings.Background;
        TextSlider.Value = _settings.Text;
        ContrastSlider.Value = _settings.Contrast;
        SaturationSlider.Value = _settings.Saturation;
        WarmthSlider.Value = _settings.Warmth;
        MidtonesSlider.Value = _settings.Midtones;
        SoftenBox.IsChecked = _settings.SoftenOneCText;
        OnlyOneCBox.IsChecked = _settings.OnlyWhenOneCActive;
        ThickClientBox.IsChecked = _settings.IncludeThickClient;
        StartupBox.IsChecked = _settings.StartWithWindows;
        TrayBox.IsChecked = _settings.CloseToTray;
        RefreshSliderCaptions();

        var theme = ThemeEngine.Resolve(_settings.ThemeId);
        ThemeBox.SelectedItem = theme;
        ThemeHint.Text = theme.Hint;

        if (MonitorBox.Items.Count > 0)
        {
            var selected = MonitorBox.Items.OfType<MonitorTarget>()
                .FirstOrDefault(m => m.DeviceName == _settings.MonitorDeviceName);
            MonitorBox.SelectedItem = selected ?? MonitorBox.Items[0];
        }

        RefreshPreview();
        _syncing = false;
    }

    private void RefreshSliderCaptions()
    {
        StrengthCaption.Text = $"Сила эффекта: {_settings.Strength}";
        BackgroundCaption.Text = $"Яркость фона: {_settings.Background}";
        TextCaption.Text = $"Яркость текста: {_settings.Text}";
        ContrastCaption.Text = $"Контраст: {_settings.Contrast}";
        SaturationCaption.Text = $"Насыщенность / сохранение цвета: {_settings.Saturation}";
        WarmthCaption.Text = $"Теплота: {_settings.Warmth}  (50 — нейтрально)";
        MidtonesCaption.Text = $"Средние тона: {_settings.Midtones}  (50 — нейтрально)";
    }

    private void ThemeBoxOnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ThemeBox.SelectedItem is not ColorThemeInfo theme)
        {
            return;
        }

        if (_settings.ThemeId == theme.Id)
        {
            ThemeHint.Text = theme.Hint;
            RefreshPreview();
            return;
        }

        ThemeEngine.ApplyDefaults(_settings, theme);
    }

    private void MonitorBoxOnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || MonitorBox.SelectedItem is not MonitorTarget monitor)
        {
            return;
        }

        _settings.MonitorDeviceName = monitor.DeviceName;
    }

    private void ReloadMonitors()
    {
        var monitors = MonitorCatalog.Enumerate();
        MonitorBox.ItemsSource = monitors;
        var selected = monitors.FirstOrDefault(m => m.DeviceName == _settings.MonitorDeviceName)
                       ?? monitors.FirstOrDefault(m => m.IsPrimary)
                       ?? monitors.FirstOrDefault();
        if (selected is not null)
        {
            MonitorBox.SelectedItem = selected;
            _settings.MonitorDeviceName = selected.DeviceName;
        }
    }

    private void RefreshPreview()
    {
        var matrix = ThemeEngine.Build(_settings);
        ChipWhite.Background = matrix.TransformBrush(255, 255, 255);
        ChipPanel.Background = matrix.TransformBrush(225, 245, 254);
        ChipText.Background = matrix.TransformBrush(51, 51, 51);
        ChipCyan.Background = matrix.TransformBrush(179, 229, 252);
        ChipGreen.Background = matrix.TransformBrush(46, 125, 50);
        ChipYellow.Background = matrix.TransformBrush(251, 192, 45);
    }

    private void UpdateStatus()
    {
        StatusText.Text = _filter.Status;
    }
}
