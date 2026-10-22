using Forms = System.Windows.Forms;

namespace ClockWidget;

public sealed class TrayController : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Program app;
    private readonly WidgetWindow window;
    private readonly System.Drawing.Icon drawingIcon;
    private readonly Forms.ToolStripMenuItem lockItem;
    private readonly Forms.ToolStripMenuItem throughItem;
    private readonly Forms.ToolStripMenuItem topItem;
    public TrayController(Program app, WidgetWindow window)
    {
        this.app = app; this.window = window;
        drawingIcon = new System.Drawing.Icon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "clock.ico"));
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show clock", null, (_, _) => app.RevealClock());
        menu.Items.Add("Settings…", null, (_, _) => app.OpenSettings());
        menu.Items.Add("Move / unlock", null, (_, _) => app.Unlock());
        lockItem = new Forms.ToolStripMenuItem("Lock position", null, (_, _) => app.UpdateSettings(app.Settings with { Locked = !app.Settings.Locked }));
        throughItem = new Forms.ToolStripMenuItem("Click-through", null, (_, _) => app.UpdateSettings(app.Settings with { ClickThrough = !app.Settings.ClickThrough }));
        topItem = new Forms.ToolStripMenuItem("Always on top", null, (_, _) => app.UpdateSettings(app.Settings with { AlwaysOnTop = !app.Settings.AlwaysOnTop }));
        menu.Items.AddRange([lockItem, throughItem, topItem]);
        menu.Items.Add("Top center", null, (_, _) => app.ResetPosition());
        menu.Items.Add("Center", null, (_, _) => app.ResetPosition(true));
        menu.Items.Add("Show / hide", null, (_, _) => app.ToggleClockVisibility());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => app.Shutdown());
        icon = new Forms.NotifyIcon { Icon = drawingIcon, Text = "Widgets", ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => app.OpenSettings(); Refresh();
    }
    public void Refresh()
    {
        lockItem.Checked = app.Settings.Locked; throughItem.Checked = app.Settings.ClickThrough; topItem.Checked = app.Settings.AlwaysOnTop;
        icon.Text = app.StorageStatus.Length > 0 ? "Widgets — settings not saved" : "Widgets";
    }
    public void Dispose() { icon.Visible = false; icon.ContextMenuStrip?.Dispose(); icon.Dispose(); drawingIcon.Dispose(); }
}
