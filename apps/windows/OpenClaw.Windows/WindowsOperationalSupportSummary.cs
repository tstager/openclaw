using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Windows;

/// <summary>
/// Serializable support snapshot for copy-to-clipboard and lightweight bundle export flows.
/// </summary>
public sealed record WindowsOperationalSupportSummary(
    DateTimeOffset GeneratedAt,
    string GatewayUrl,
    bool StructuredDiagnosticsEnabled,
    string DiagnosticsPath,
    string ActivityHistoryPath,
    string NotificationHistoryPath,
    int ActivityRetentionCount,
    int NotificationHistoryRetentionCount,
    IReadOnlyList<WindowsActivityEntry> RecentActivity,
    IReadOnlyList<WindowsNotificationActivity> RecentNotifications,
    IReadOnlyList<WindowsNotificationRule> NotificationRules)
{
    public string ToPlainText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("OpenClaw Windows operational summary");
        builder.AppendLine($"Generated at: {this.GeneratedAt:O}");
        builder.AppendLine($"Gateway URL: {this.GatewayUrl}");
        builder.AppendLine($"Structured diagnostics enabled: {this.StructuredDiagnosticsEnabled}");
        builder.AppendLine($"Structured diagnostics path: {this.DiagnosticsPath}");
        builder.AppendLine($"Activity history path: {this.ActivityHistoryPath}");
        builder.AppendLine($"Notification history path: {this.NotificationHistoryPath}");
        builder.AppendLine($"Activity retention count: {this.ActivityRetentionCount}");
        builder.AppendLine($"Notification history retention count: {this.NotificationHistoryRetentionCount}");
        builder.AppendLine($"Stored notification rules: {this.NotificationRules.Count}");

        AppendRules(builder, this.NotificationRules);
        AppendActivities(builder, this.RecentActivity);
        AppendNotifications(builder, this.RecentNotifications);
        return builder.ToString().TrimEnd();
    }

    private static void AppendRules(StringBuilder builder, IReadOnlyList<WindowsNotificationRule> rules)
    {
        builder.AppendLine();
        builder.AppendLine("Notification rules:");
        if (rules.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var rule in rules)
        {
            builder.AppendLine(
                $"- {rule.Id}: kind={rule.Kind}, category={rule.Category}, destination={rule.Destination}, enabled={rule.Enabled}");
        }
    }

    private static void AppendActivities(StringBuilder builder, IReadOnlyList<WindowsActivityEntry> activities)
    {
        builder.AppendLine();
        builder.AppendLine("Recent activity:");
        if (activities.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var entry in activities)
        {
            builder.AppendLine(
                $"- [{entry.CreatedAt:O}] {entry.Category}: {entry.Title} ({entry.Destination ?? "none"}) - {entry.Detail}");
        }
    }

    private static void AppendNotifications(StringBuilder builder, IReadOnlyList<WindowsNotificationActivity> notifications)
    {
        builder.AppendLine();
        builder.AppendLine("Recent notifications:");
        if (notifications.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var entry in notifications)
        {
            builder.AppendLine(
                $"- [{entry.CreatedAt:O}] {entry.Kind}/{entry.Category}: {entry.Title} ({entry.Destination}) - {entry.Message}");
        }
    }
}

/// <summary>
/// Builds and optionally persists compact operational summaries from existing Windows companion stores.
/// </summary>
public sealed class WindowsOperationalSupportSummaryBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    public WindowsOperationalSupportSummary Build(
        AppPreferences preferences,
        WindowsStructuredDiagnosticsWriter diagnostics,
        WindowsActivityHistoryStore activityHistory,
        WindowsNotificationHistoryStore notificationHistory,
        int recentEntryCount = 10)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(activityHistory);
        ArgumentNullException.ThrowIfNull(notificationHistory);

        var takeCount = recentEntryCount <= 0 ? 10 : recentEntryCount;
        var notificationRules = WindowsNotificationRuleEvaluator.NormalizePreferences(preferences.NotificationRules);

        return new WindowsOperationalSupportSummary(
            GeneratedAt: DateTimeOffset.Now,
            GatewayUrl: preferences.GatewayUrl,
            StructuredDiagnosticsEnabled: preferences.Diagnostics.StructuredDiagnosticsEnabled,
            DiagnosticsPath: diagnostics.ResolvePath(preferences.Diagnostics.StructuredDiagnosticsPath),
            ActivityHistoryPath: activityHistory.Path,
            NotificationHistoryPath: notificationHistory.Path,
            ActivityRetentionCount: preferences.Diagnostics.ActivityRetentionCount,
            NotificationHistoryRetentionCount: notificationRules.HistoryRetentionCount,
            RecentActivity: activityHistory.Entries.Take(takeCount).ToArray(),
            RecentNotifications: notificationHistory.Entries.Take(takeCount).ToArray(),
            NotificationRules: notificationRules.Rules.ToArray());
    }

    public async Task WriteArtifactAsync(
        string path,
        WindowsOperationalSupportSummary summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(summary);

        var directory = System.IO.Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var tempPath = System.IO.Path.Combine(
            directory,
            $"{System.IO.Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, summary, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
