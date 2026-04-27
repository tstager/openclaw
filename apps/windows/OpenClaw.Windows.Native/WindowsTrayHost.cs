using System.Drawing;
using System.Windows.Forms;

namespace OpenClaw.Windows.Native;

public sealed class WindowsTrayHost : IDisposable
{
    private readonly NotifyIcon notifyIcon;

    public WindowsTrayHost(
        Action onShow,
        Action onInstallGateway,
        Action onStartGateway,
        Action onRestartGateway,
        Action onStopGateway,
        Action onExit)
    {
        this.notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "OpenClaw",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };
        this.notifyIcon.ContextMenuStrip.Items.Add("Open OpenClaw", null, (_, _) => onShow());
        this.notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        this.notifyIcon.ContextMenuStrip.Items.Add("Install Gateway", null, (_, _) => onInstallGateway());
        this.notifyIcon.ContextMenuStrip.Items.Add("Start Gateway", null, (_, _) => onStartGateway());
        this.notifyIcon.ContextMenuStrip.Items.Add("Restart Gateway", null, (_, _) => onRestartGateway());
        this.notifyIcon.ContextMenuStrip.Items.Add("Stop Gateway", null, (_, _) => onStopGateway());
        this.notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        this.notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => onExit());
        this.notifyIcon.DoubleClick += (_, _) => onShow();
    }

    public void Dispose()
    {
        this.notifyIcon.Visible = false;
        this.notifyIcon.ContextMenuStrip?.Dispose();
        this.notifyIcon.Dispose();
    }
}
