namespace OpenClaw.Windows;

public static class WindowsNavigationDestination
{
    public const string Home = "home";
    public const string Sessions = "sessions";
    public const string Approvals = "approvals";
    public const string Pairing = "pairing";
    public const string Devices = "devices";
    public const string Logs = "logs";
    public const string Settings = "settings";
    public const string Diagnostics = "diagnostics";
}

public sealed record WindowsNavigationItem(string Label, string Destination, string Glyph);

public sealed class WindowsNavigationService
{
    public IReadOnlyList<WindowsNavigationItem> PrimaryItems { get; } =
    [
        new("Home", WindowsNavigationDestination.Home, "\uE80F"),
        new("Sessions", WindowsNavigationDestination.Sessions, "\uE8BD"),
        new("Approvals", WindowsNavigationDestination.Approvals, "\uE73E"),
        new("Pairing", WindowsNavigationDestination.Pairing, "\uE71B"),
        new("Devices", WindowsNavigationDestination.Devices, "\uE722"),
        new("Logs", WindowsNavigationDestination.Logs, "\uE8A5"),
    ];

    public static string Normalize(string? destination)
    {
        return destination switch
        {
            WindowsNavigationDestination.Home => WindowsNavigationDestination.Home,
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

    public static string PageTitle(string destination)
    {
        return Normalize(destination) switch
        {
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
