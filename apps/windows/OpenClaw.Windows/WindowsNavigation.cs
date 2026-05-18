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
    public IReadOnlyList<WindowsNavigationItem> PrimaryItems { get; } =
    [
        new("Home", WindowsNavigationDestination.Home, "\uE80F"),
        new("Chat", WindowsNavigationDestination.Chat, "\uE8F2"),
        new("Canvas", WindowsNavigationDestination.Canvas, "\uE7F4"),
        new("Sessions", WindowsNavigationDestination.Sessions, "\uE8BD"),
        new("Approvals", WindowsNavigationDestination.Approvals, "\uE73E"),
        new("Pairing", WindowsNavigationDestination.Pairing, "\uE71B"),
        new("Devices", WindowsNavigationDestination.Devices, "\uE722"),
        new("Logs", WindowsNavigationDestination.Logs, "\uE8A5"),
    ];

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
    public static string PageTitle(string destination)
    {
        return Normalize(destination) switch
        {
            WindowsNavigationDestination.Chat => "Chat",
            WindowsNavigationDestination.Canvas => "Canvas",
            WindowsNavigationDestination.Sessions => "Sessions",
            WindowsNavigationDestination.Approvals => "Approvals",
            WindowsNavigationDestination.Pairing => "Pairing",
            WindowsNavigationDestination.Devices => "Devices",
            WindowsNavigationDestination.Logs => "Logs",
            WindowsNavigationDestination.Settings => "Settings",
            _ => "Home",
        };
    }
}
