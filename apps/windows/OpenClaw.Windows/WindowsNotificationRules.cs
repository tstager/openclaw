namespace OpenClaw.Windows;

/// <summary>
/// One stored notification categorization rule editable by future settings surfaces.
/// </summary>
public sealed record WindowsNotificationRule(
    string Id,
    WindowsNotificationKind Kind,
    string Category,
    string Destination,
    bool Enabled);

/// <summary>
/// Persisted notification categorization and history retention preferences.
/// </summary>
public sealed record WindowsNotificationRulePreferences(
    int HistoryRetentionCount,
    IReadOnlyList<WindowsNotificationRule> Rules)
{
    public static WindowsNotificationRulePreferences Default { get; } = new(
        HistoryRetentionCount: 100,
        Rules:
        [
            new(
                Id: "approval-default",
                Kind: WindowsNotificationKind.Approval,
                Category: WindowsNotificationCategories.Operator,
                Destination: WindowsNavigationDestination.Approvals,
                Enabled: true),
            new(
                Id: "pairing-default",
                Kind: WindowsNotificationKind.Pairing,
                Category: WindowsNotificationCategories.Operator,
                Destination: WindowsNavigationDestination.Pairing,
                Enabled: true),
            new(
                Id: "gateway-health-default",
                Kind: WindowsNotificationKind.GatewayHealth,
                Category: WindowsNotificationCategories.Gateway,
                Destination: WindowsNavigationDestination.Home,
                Enabled: true),
            new(
                Id: "device-permission-default",
                Kind: WindowsNotificationKind.DevicePermission,
                Category: WindowsNotificationCategories.Device,
                Destination: WindowsNavigationDestination.Devices,
                Enabled: true),
        ]);
}

/// <summary>
/// The resolved classification for a notification after applying stored rules and defaults.
/// </summary>
public sealed record WindowsNotificationClassification(
    WindowsNotificationKind Kind,
    string Category,
    string Destination,
    string? RuleId);

/// <summary>
/// Resolves stored notification rules into stable categories and destinations for history UX.
/// </summary>
public sealed class WindowsNotificationRuleEvaluator
{
    public WindowsNotificationClassification Classify(
        WindowsNotificationKind kind,
        string? destination,
        IReadOnlyList<WindowsNotificationRule>? rules)
    {
        var fallbackDestination = NormalizeDestination(kind, destination);
        var fallbackCategory = DefaultCategory(kind);
        var matchingRule = rules?
            .Select(NormalizeRule)
            .FirstOrDefault(rule => rule.Enabled && rule.Kind == kind);

        if (matchingRule is null)
        {
            return new WindowsNotificationClassification(kind, fallbackCategory, fallbackDestination, null);
        }

        return new WindowsNotificationClassification(
            kind,
            matchingRule.Category,
            string.IsNullOrWhiteSpace(matchingRule.Destination)
                ? fallbackDestination
                : WindowsNavigationService.Normalize(matchingRule.Destination),
            matchingRule.Id);
    }

    public static WindowsNotificationRulePreferences NormalizePreferences(WindowsNotificationRulePreferences? preferences)
    {
        if (preferences is null)
        {
            return WindowsNotificationRulePreferences.Default;
        }

        return new WindowsNotificationRulePreferences(
            HistoryRetentionCount: preferences.HistoryRetentionCount <= 0
                ? WindowsNotificationRulePreferences.Default.HistoryRetentionCount
                : preferences.HistoryRetentionCount,
            Rules: (preferences.Rules ?? [])
                .Select(NormalizeRule)
                .Where(static rule => !string.IsNullOrWhiteSpace(rule.Id))
                .DistinctBy(static rule => rule.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static WindowsNotificationRule NormalizeRule(WindowsNotificationRule rule)
    {
        return rule with
        {
            Id = rule.Id.Trim(),
            Category = string.IsNullOrWhiteSpace(rule.Category)
                ? DefaultCategory(rule.Kind)
                : WindowsNotificationCategories.Normalize(rule.Category),
            Destination = NormalizeDestination(rule.Kind, rule.Destination),
        };
    }

    private static string NormalizeDestination(WindowsNotificationKind kind, string? destination)
    {
        if (!string.IsNullOrWhiteSpace(destination))
        {
            return WindowsNavigationService.Normalize(destination.Trim());
        }

        return kind switch
        {
            WindowsNotificationKind.Approval => WindowsNavigationDestination.Approvals,
            WindowsNotificationKind.Pairing => WindowsNavigationDestination.Pairing,
            WindowsNotificationKind.GatewayHealth => WindowsNavigationDestination.Home,
            WindowsNotificationKind.DevicePermission => WindowsNavigationDestination.Devices,
            _ => WindowsNavigationDestination.Home,
        };
    }

    private static string DefaultCategory(WindowsNotificationKind kind)
    {
        return kind switch
        {
            WindowsNotificationKind.Approval => WindowsNotificationCategories.Operator,
            WindowsNotificationKind.Pairing => WindowsNotificationCategories.Operator,
            WindowsNotificationKind.GatewayHealth => WindowsNotificationCategories.Gateway,
            WindowsNotificationKind.DevicePermission => WindowsNotificationCategories.Device,
            _ => WindowsNotificationCategories.General,
        };
    }
}
