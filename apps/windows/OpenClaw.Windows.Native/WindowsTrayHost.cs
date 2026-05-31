using System.Drawing;
using System.Windows.Forms;

namespace OpenClaw.Windows.Native;

/// <summary>
/// Screen position, in physical pixels, where the tray flyout should be anchored.
/// </summary>
public readonly record struct TrayAnchorPoint(int X, int Y);

/// <summary>
/// WinForms <see cref="NotifyIcon"/> bridge used because WinUI 3 has no built-in tray icon API.
/// The visible tray UX is a WinUI flyout owned by the app; this host only manages the icon and
/// raises an event when the operator asks to open that flyout.
/// </summary>
public sealed class WindowsTrayHost : IDisposable
{
    private readonly NotifyIcon notifyIcon;
    private readonly Action onShow;
    private readonly Action onNotificationClicked;

    /// <summary>
    /// Creates the tray icon. <paramref name="onShow"/> opens the main app on double-click; the
    /// owner shows the WinUI flyout in response to <see cref="TrayActivated"/>.
    /// </summary>
    public WindowsTrayHost(Action onShow, Action onNotificationClicked)
    {
        this.onShow = onShow;
        this.onNotificationClicked = onNotificationClicked;
        this.notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "OpenClaw",
            Visible = true,
        };
        this.notifyIcon.MouseUp += this.OnMouseUp;
        this.notifyIcon.DoubleClick += (_, _) => this.onShow();
        this.notifyIcon.BalloonTipClicked += (_, _) => this.onNotificationClicked();
    }

    /// <summary>
    /// Raised when the operator left- or right-clicks the tray icon so the owner can show the flyout
    /// anchored at the supplied screen position.
    /// </summary>
    public event Action<TrayAnchorPoint>? TrayActivated;

    /// <summary>
    /// Updates the tray icon tooltip text shown on hover.
    /// </summary>
    public void SetTooltip(string text)
    {
        // NotifyIcon truncates tooltips past 63 chars and throws on longer values, so guard the length.
        this.notifyIcon.Text = text.Length > 63 ? text[..63] : text;
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
        this.notifyIcon.Dispose();
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button is not (MouseButtons.Left or MouseButtons.Right))
        {
            return;
        }

        var cursor = Cursor.Position;
        this.TrayActivated?.Invoke(new TrayAnchorPoint(cursor.X, cursor.Y));
    }
}
