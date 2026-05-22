using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsNotificationRuleEvaluatorTests
{
    [TestMethod]
    public void Classify_UsesFallbacksWhenNoRuleMatches()
    {
        var evaluator = new WindowsNotificationRuleEvaluator();

        var classification = evaluator.Classify(WindowsNotificationKind.Unknown, WindowsNavigationDestination.Logs, []);

        Assert.AreEqual(WindowsNotificationCategories.General, classification.Category);
        Assert.AreEqual(WindowsNavigationDestination.Logs, classification.Destination);
        Assert.IsNull(classification.RuleId);
    }

    [TestMethod]
    public void Classify_UsesEnabledRuleOverride()
    {
        var evaluator = new WindowsNotificationRuleEvaluator();
        IReadOnlyList<WindowsNotificationRule> rules =
        [
            new WindowsNotificationRule(
                "approval-custom",
                WindowsNotificationKind.Approval,
                "triage",
                WindowsNavigationDestination.Logs,
                Enabled: true),
        ];

        var classification = evaluator.Classify(WindowsNotificationKind.Approval, WindowsNavigationDestination.Home, rules);

        Assert.AreEqual("triage", classification.Category);
        Assert.AreEqual(WindowsNavigationDestination.Logs, classification.Destination);
        Assert.AreEqual("approval-custom", classification.RuleId);
    }

    [TestMethod]
    public void NormalizePreferences_TrimsAndDeduplicatesRules()
    {
        var preferences = new WindowsNotificationRulePreferences(
            HistoryRetentionCount: 0,
            Rules:
            [
                new WindowsNotificationRule(" approval ", WindowsNotificationKind.Approval, " Operator ", " approvals ", true),
                new WindowsNotificationRule("approval", WindowsNotificationKind.Approval, "ignored", "logs", true),
            ]);

        var normalized = WindowsNotificationRuleEvaluator.NormalizePreferences(preferences);

        Assert.AreEqual(WindowsNotificationRulePreferences.Default.HistoryRetentionCount, normalized.HistoryRetentionCount);
        Assert.HasCount(1, normalized.Rules);
        Assert.AreEqual("approval", normalized.Rules[0].Id);
        Assert.AreEqual(WindowsNotificationCategories.Operator, normalized.Rules[0].Category);
        Assert.AreEqual(WindowsNavigationDestination.Approvals, normalized.Rules[0].Destination);
    }
}
