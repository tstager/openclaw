namespace OpenClaw.Windows;

/// <summary>
/// Stable navigation tags used by the shell, tray actions, and notification routing.
/// </summary>
public static class WindowsNavigationDestination
{
    public const string Home = "home";
    public const string Chat = "chat";
    public const string Canvas = "canvas";
    public const string Sessions = "sessions";
    public const string Approvals = "approvals";
    public const string Pairing = "pairing";
    public const string Devices = "devices";
    public const string Logs = "logs";
    public const string Settings = "settings";
    public const string Diagnostics = "diagnostics";
}

/// <summary>
/// Defines a shell navigation entry and the Segoe Fluent icon glyph shown for it.
/// </summary>
public sealed record WindowsNavigationItem(string Label, string Destination, string Glyph);

/// <summary>
/// Centralizes navigation item order and normalizes external destinations to known pages.
/// </summary>
public sealed class WindowsNavigationService
{
    private readonly IWindowsStringLocalizer localizer;

    public WindowsNavigationService(IWindowsStringLocalizer? localizer = null)
    {
        this.localizer = localizer ?? new WindowsStringLocalizer();
        this.PrimaryItems =
        [
            this.CreatePrimaryItem(WindowsNavigationDestination.Home, "\uE80F"),
            this.CreatePrimaryItem(WindowsNavigationDestination.Chat, "\uE8F2"),
            this.CreatePrimaryItem(WindowsNavigationDestination.Canvas, "\uE7F4"),
            this.CreatePrimaryItem(WindowsNavigationDestination.Sessions, "\uE8BD"),
            this.CreatePrimaryItem(WindowsNavigationDestination.Approvals, "\uE73E"),
            this.CreatePrimaryItem(WindowsNavigationDestination.Pairing, "\uE71B"),
            this.CreatePrimaryItem(WindowsNavigationDestination.Devices, "\uE722"),
            this.CreatePrimaryItem(WindowsNavigationDestination.Logs, "\uE8A5"),
        ];
    }

    public IReadOnlyList<WindowsNavigationItem> PrimaryItems { get; }

    /// <summary>
    /// Maps unknown, legacy, or alias destinations to the closest supported page.
    /// </summary>
    public static string Normalize(string? destination)
    {
        return destination switch
        {
            WindowsNavigationDestination.Home => WindowsNavigationDestination.Home,
            WindowsNavigationDestination.Chat => WindowsNavigationDestination.Chat,
            WindowsNavigationDestination.Canvas => WindowsNavigationDestination.Canvas,
            WindowsNavigationDestination.Sessions => WindowsNavigationDestination.Sessions,
            WindowsNavigationDestination.Approvals => WindowsNavigationDestination.Approvals,
            WindowsNavigationDestination.Pairing => WindowsNavigationDestination.Pairing,
            WindowsNavigationDestination.Devices => WindowsNavigationDestination.Devices,
            WindowsNavigationDestination.Logs => WindowsNavigationDestination.Logs,
            WindowsNavigationDestination.Settings => WindowsNavigationDestination.Settings,
            WindowsNavigationDestination.Diagnostics => WindowsNavigationDestination.Logs,
            _ => WindowsNavigationDestination.Home,
        };
    }

    /// <summary>
    /// Returns the visible page heading for a normalized destination.
    /// </summary>
    public string PageTitle(string destination)
    {
        return Normalize(destination) switch
        {
            WindowsNavigationDestination.Chat => this.localizer.Get("Shell.Navigation.Chat", "Chat"),
            WindowsNavigationDestination.Canvas => this.localizer.Get("Shell.Navigation.Canvas", "Canvas"),
            WindowsNavigationDestination.Sessions => this.localizer.Get("Shell.Navigation.Sessions", "Sessions"),
            WindowsNavigationDestination.Approvals => this.localizer.Get("Shell.Navigation.Approvals", "Approvals"),
            WindowsNavigationDestination.Pairing => this.localizer.Get("Shell.Navigation.Pairing", "Pairing"),
            WindowsNavigationDestination.Devices => this.localizer.Get("Shell.Navigation.Devices", "Devices"),
            WindowsNavigationDestination.Logs => this.localizer.Get("Shell.Navigation.Logs", "Logs"),
            WindowsNavigationDestination.Settings => this.localizer.Get("Shell.Navigation.Settings", "Settings"),
            _ => this.localizer.Get("Shell.Navigation.Home", "Home"),
        };
    }

    private WindowsNavigationItem CreatePrimaryItem(string destination, string glyph)
    {
        return new WindowsNavigationItem(this.PageTitle(destination), destination, glyph);
    }
}
