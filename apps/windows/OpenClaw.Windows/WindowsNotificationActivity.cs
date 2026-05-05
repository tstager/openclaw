namespace OpenClaw.Windows;

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

public sealed record WindowsNotificationActivity(
    DateTimeOffset CreatedAt,
    string Destination,
    string Title,
    string Message);

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

    public WindowsNotificationActivity Add(string destination, string title, string message)
    {
        var entry = new WindowsNotificationActivity(DateTimeOffset.Now, destination, title, message);
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
}
