using System.Drawing;
using System.Windows.Forms;

namespace OpenClaw.Windows.Native;

/// <summary>
/// WinForms NotifyIcon bridge used because WinUI 3 has no built-in tray icon API.
/// </summary>
public sealed class WindowsTrayHost : IDisposable
{
    private readonly NotifyIcon notifyIcon;

    public WindowsTrayHost(
        Func<string> getGatewayStatus,
        Func<string> getLatestActivity,
        Action onShow,
        Action onShowHome,
        Action onShowLogs,
        Action onShowSettings,
        Action onInstallGateway,
        Action onStartGateway,
        Action onRestartGateway,
        Action onStopGateway,
        Action onConnect,
        Action onOpenLogs,
        Action onNotificationClicked,
        Action onExit)
    {
        // Menu status rows are refreshed lazily so tray state reflects the latest coordinator values.
        var statusItem = new ToolStripMenuItem("Gateway: Unknown")
        {
            Enabled = false,
        };
        var activityItem = new ToolStripMenuItem("Activity: None")
        {
            Enabled = false,
        };
        this.notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "OpenClaw",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };
        this.notifyIcon.ContextMenuStrip.Opening += (_, _) =>
        {
            statusItem.Text = $"Gateway: {getGatewayStatus()}";
            activityItem.Text = $"Activity: {getLatestActivity()}";
        };
        this.notifyIcon.ContextMenuStrip.Items.Add(statusItem);
        this.notifyIcon.ContextMenuStrip.Items.Add(activityItem);
        this.notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        this.notifyIcon.ContextMenuStrip.Items.Add("Open OpenClaw", null, (_, _) => onShow());
        this.notifyIcon.ContextMenuStrip.Items.Add("Home", null, (_, _) => onShowHome());
        this.notifyIcon.ContextMenuStrip.Items.Add("Logs", null, (_, _) => onShowLogs());
        this.notifyIcon.ContextMenuStrip.Items.Add("Settings", null, (_, _) => onShowSettings());
        this.notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        this.notifyIcon.ContextMenuStrip.Items.Add("Install Gateway", null, (_, _) => onInstallGateway());
        this.notifyIcon.ContextMenuStrip.Items.Add("Start Gateway", null, (_, _) => onStartGateway());
        this.notifyIcon.ContextMenuStrip.Items.Add("Restart Gateway", null, (_, _) => onRestartGateway());
        this.notifyIcon.ContextMenuStrip.Items.Add("Stop Gateway", null, (_, _) => onStopGateway());
        this.notifyIcon.ContextMenuStrip.Items.Add("Connect", null, (_, _) => onConnect());
        this.notifyIcon.ContextMenuStrip.Items.Add("Open Logs", null, (_, _) => onOpenLogs());
        this.notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        this.notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => onExit());
        this.notifyIcon.DoubleClick += (_, _) => onShow();
        this.notifyIcon.BalloonTipClicked += (_, _) => onNotificationClicked();
    }

    /// <summary>
    /// Shows a Windows balloon notification through the tray icon.
    /// </summary>
    public void ShowNotification(string title, string message)
    {
        this.notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        this.notifyIcon.Visible = false;
        this.notifyIcon.ContextMenuStrip?.Dispose();
        this.notifyIcon.Dispose();
    }
}
