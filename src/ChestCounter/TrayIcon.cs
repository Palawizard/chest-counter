namespace ChestCounter;

// The icon by the clock: open the window, hide or show the overlay, quit for real.
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _overlay;

    public TrayIcon(Icon icon, Action open, Action toggleOverlay, Action quit)
    {
        _overlay = new ToolStripMenuItem(Text.Pick("Overlay en jeu", "In-game overlay"), null, (_, _) => toggleOverlay());
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(Text.Pick("Ouvrir Chest Counter", "Open Chest Counter"), null, (_, _) => open()) { Font = new Font(menu.Font, FontStyle.Bold) });
        menu.Items.Add(_overlay);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Text.Pick("Quitter", "Quit"), null, (_, _) => quit()));
        _icon = new NotifyIcon { Icon = icon, Text = Text.AppName, ContextMenuStrip = menu, Visible = true };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                open();
            }
        };
        _icon.BalloonTipClicked += (_, _) => open();
    }

    public bool OverlayOn
    {
        set => _overlay.Checked = value;
    }

    public void Announce(string title, string text) => _icon.ShowBalloonTip(5000, title, text, ToolTipIcon.None);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
