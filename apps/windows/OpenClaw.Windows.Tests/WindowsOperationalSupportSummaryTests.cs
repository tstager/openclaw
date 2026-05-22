using System.Text.Json;
using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsOperationalSupportSummaryTests
{
    [TestMethod]
    public async Task Build_IncludesResolvedPathsAndRecentContext()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var activityHistory = new WindowsActivityHistoryStore(Path.Combine(root, "activity-history.json"));
        var notificationHistory = new WindowsNotificationHistoryStore(Path.Combine(root, "notification-history.json"));
        var diagnostics = new WindowsStructuredDiagnosticsWriter(Path.Combine(root, "diagnostics.jsonl"));
        var builder = new WindowsOperationalSupportSummaryBuilder();
        var preferences = AppPreferences.Default with
        {
            GatewayUrl = "ws://127.0.0.1:18888",
            Diagnostics = AppPreferences.Default.Diagnostics with
            {
                StructuredDiagnosticsPath = Path.Combine(root, "custom-diagnostics.jsonl"),
                ActivityRetentionCount = 250,
            },
            NotificationRules = new WindowsNotificationRulePreferences(
                HistoryRetentionCount: 25,
                Rules:
                [
                    new WindowsNotificationRule(
                        "approval-custom",
                        WindowsNotificationKind.Approval,
                        "triage",
                        WindowsNavigationDestination.Approvals,
                        true),
                ]),
        };

        await activityHistory.AddAsync("gateway", "Started", "Gateway started.", WindowsNavigationDestination.Home, 10);
        await notificationHistory.AddAsync(
            WindowsNavigationDestination.Approvals,
            "OpenClaw approval",
            "1 approval request pending.",
            capacity: 10,
            category: "triage",
            kind: WindowsNotificationKind.Approval);

        var summary = builder.Build(preferences, diagnostics, activityHistory, notificationHistory, recentEntryCount: 5);

        Assert.AreEqual(preferences.GatewayUrl, summary.GatewayUrl);
        Assert.AreEqual(preferences.Diagnostics.StructuredDiagnosticsPath, summary.DiagnosticsPath);
        Assert.AreEqual(activityHistory.Path, summary.ActivityHistoryPath);
        Assert.AreEqual(notificationHistory.Path, summary.NotificationHistoryPath);
        Assert.HasCount(1, summary.RecentActivity);
        Assert.HasCount(1, summary.RecentNotifications);
        StringAssert.Contains(summary.ToPlainText(), "approval-custom");
        StringAssert.Contains(summary.ToPlainText(), "OpenClaw approval");
    }

    [TestMethod]
    public async Task WriteArtifactAsync_WritesJsonBundle()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var builder = new WindowsOperationalSupportSummaryBuilder();
        var summary = new WindowsOperationalSupportSummary(
            DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            "ws://127.0.0.1:18789",
            true,
            Path.Combine(root, "diagnostics.jsonl"),
            Path.Combine(root, "activity-history.json"),
            Path.Combine(root, "notification-history.json"),
            200,
            100,
            [],
            [],
            []);
        var artifactPath = Path.Combine(root, "support-summary.json");

        await builder.WriteArtifactAsync(artifactPath, summary);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(artifactPath));
        Assert.AreEqual("ws://127.0.0.1:18789", document.RootElement.GetProperty("gatewayUrl").GetString());
        Assert.AreEqual(100, document.RootElement.GetProperty("notificationHistoryRetentionCount").GetInt32());
    }
}
