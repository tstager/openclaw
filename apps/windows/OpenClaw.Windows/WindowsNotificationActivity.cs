namespace OpenClaw.Windows;

/// <summary>
/// Stable notification kinds that later UX surfaces can filter and categorize.
/// </summary>
public enum WindowsNotificationKind
{
    Unknown,
    Approval,
    Pairing,
    GatewayHealth,
    DevicePermission,
}

/// <summary>
/// Shared notification category labels used by persisted history and future filters.
/// </summary>
public static class WindowsNotificationCategories
{
    public const string General = "general";
    public const string Operator = "operator";
    public const string Gateway = "gateway";
    public const string Device = "device";

    public static string Normalize(string? category)
    {
        return string.IsNullOrWhiteSpace(category)
            ? General
            : category.Trim().ToLowerInvariant();
    }
}

/// <summary>
/// Stores per-category notification toggles for Windows companion alerts.
/// </summary>
public sealed record WindowsNotificationPreferences(
    bool ApprovalAlerts,
    bool PairingAlerts,
    bool GatewayHealthAlerts,
    bool DevicePermissionAlerts)
{
    public static WindowsNotificationPreferences Default { get; } = new(
        ApprovalAlerts: true,
        PairingAlerts: true,
        GatewayHealthAlerts: true,
        DevicePermissionAlerts: true);
}

/// <summary>
/// Captures one notification shown by the tray host so the UI can display recent activity.
/// </summary>
public sealed record WindowsNotificationActivity(
    DateTimeOffset CreatedAt,
    string Destination,
    string Title,
    string Message,
    string Category = WindowsNotificationCategories.General,
    WindowsNotificationKind Kind = WindowsNotificationKind.Unknown);

/// <summary>
/// Thread-safe bounded in-memory notification history for tray click routing and diagnostics.
/// </summary>
public sealed class WindowsNotificationActivityLog(int capacity = 10)
{
    private readonly object gate = new();
    private readonly List<WindowsNotificationActivity> entries = [];

    public IReadOnlyList<WindowsNotificationActivity> Entries
    {
        get
        {
            lock (this.gate)
            {
                return this.entries.ToArray();
            }
        }
    }

    public WindowsNotificationActivity? Latest
    {
        get
        {
            lock (this.gate)
            {
                return this.entries.FirstOrDefault();
            }
        }
    }

    /// <summary>
    /// Adds the newest notification and trims older entries past the configured capacity.
    /// </summary>
    public WindowsNotificationActivity Add(
        string destination,
        string title,
        string message,
        string category = WindowsNotificationCategories.General,
        WindowsNotificationKind kind = WindowsNotificationKind.Unknown)
    {
        var entry = new WindowsNotificationActivity(
            DateTimeOffset.Now,
            NormalizeDestination(destination),
            title,
            message,
            WindowsNotificationCategories.Normalize(category),
            kind);
        lock (this.gate)
        {
            this.entries.Insert(0, entry);
            if (this.entries.Count > capacity)
            {
                this.entries.RemoveRange(capacity, this.entries.Count - capacity);
            }
        }

        return entry;
    }

    /// <summary>
    /// Removes all in-memory entries so the shell can clear recent notifications without restarting.
    /// </summary>
    public void Clear()
    {
        lock (this.gate)
        {
            this.entries.Clear();
        }
    }

    private static string NormalizeDestination(string destination)
    {
        return string.IsNullOrWhiteSpace(destination)
            ? WindowsNavigationDestination.Home
            : WindowsNavigationService.Normalize(destination.Trim());
    }
}
