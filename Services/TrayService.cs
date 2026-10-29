using System.Drawing;
using System.Windows.Forms;

namespace ScreenDarkFilter.Services;

internal sealed class TrayService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly FilterService _filter;
    private readonly NotifyIcon _icon;
    private readonly Icon _ownedIcon;
    private readonly Action _showWindow;
    private readonly Action _exit;

    public TrayService(AppSettings settings, FilterService filter, Action showWindow, Action exit)
    {
        _settings = settings;
        _filter = filter;
        _showWindow = showWindow;
        _exit = exit;
        _ownedIcon = LoadIcon();

        _icon = new NotifyIcon
        {
            Icon = _ownedIcon,
            Visible = true,
            Text = "ScreenDarkFilter"
        };

        _icon.DoubleClick += (_, _) => _showWindow();
        _icon.ContextMenuStrip = BuildMenu();
        _settings.Changed += (_, _) => RefreshText();
        _filter.StatusChanged += (_, _) => RefreshText();
        RefreshText();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _ownedIcon.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть настройки", null, (_, _) => _showWindow());
        menu.Items.Add("Включить / выключить", null, (_, _) => _filter.Toggle());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Сбросить фильтр", null, (_, _) => _filter.Disable());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => _exit());
        return menu;
    }

    private void RefreshText()
    {
        string state = _settings.Enabled ? "включён" : "выключен";
        _icon.Text = $"ScreenDarkFilter ({state})";
    }

    private static Icon LoadIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                Icon? extracted = Icon.ExtractAssociatedIcon(exe);
                if (extracted is not null)
                {
                    return extracted;
                }
            }
        }
        catch
        {
            // fallback below
        }

        return CreateIcon();
    }

    private static Icon CreateIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.FromArgb(18, 22, 26));
            using var fill = new SolidBrush(System.Drawing.Color.FromArgb(47, 158, 140));
            g.FillEllipse(fill, 4, 4, 24, 24);
            using var core = new SolidBrush(System.Drawing.Color.FromArgb(22, 28, 32));
            g.FillEllipse(core, 11, 8, 16, 16);
        }

        IntPtr handle = bitmap.GetHicon();
        var icon = Icon.FromHandle(handle);
        bitmap.Dispose();
        return icon;
    }
}
