namespace OpenClaw.Windows;

/// <summary>
/// Local-only command risk heuristics used by the Windows companion approval policy.
/// </summary>
public static class WindowsApprovalPolicyEvaluator
{
    private static readonly string[] RiskyCommandFragments =
    [
        "rm ",
        "rm -",
        "rmdir",
        "del ",
        "erase ",
        "format ",
        "shutdown",
        "reboot",
        "restart-computer",
        "stop-computer",
        "curl ",
        "invoke-webrequest",
        "powershell -enc",
        "chmod 777",
        "git push --force",
    ];

    public static bool IsRisky(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return true;
        }

        return RiskyCommandFragments.Any(fragment =>
            command.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public static bool ShouldAutoAllow(WindowsPolicyPreferences policy, string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        if (policy.RememberedAllowedCommands.Contains(command, StringComparer.Ordinal))
        {
            return true;
        }

        return policy.ApprovalPolicy == WindowsApprovalPolicyPreference.AllowSafeCommands &&
            !IsRisky(command);
    }

    public static bool ShouldAutoDeny(WindowsPolicyPreferences policy, string? command)
    {
        return policy.ApprovalPolicy == WindowsApprovalPolicyPreference.DenyRiskyCommands &&
            IsRisky(command);
    }
}
