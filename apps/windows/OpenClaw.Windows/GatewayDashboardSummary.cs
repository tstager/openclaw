namespace OpenClaw.Windows;

public sealed record GatewayDashboardSummary(
    string GatewayState,
    string ServiceState,
    string Reachability,
    string Capability,
    string ConnectionState,
    string OnboardingHealth,
    string HealthState,
    string DashboardUrl,
    string LogPath)
{
    public static GatewayDashboardSummary Create(
        GatewayStatusSnapshot? status,
        GatewayRealtimeState realtimeState,
        IReadOnlyList<OnboardingCheckResult> onboardingChecks,
        GatewayRealtimeAuthorization? realtimeAuthorization = null)
    {
        var passed = onboardingChecks.Count(check => check.State == OnboardingCheckState.Passed);
        var warnings = onboardingChecks.Count(check => check.State == OnboardingCheckState.Warning);
        var failed = onboardingChecks.Count(check => check.State == OnboardingCheckState.Failed);
        var healthState = CreateHealthState(status, warnings, failed);

        return new GatewayDashboardSummary(
            GatewayState: status?.State ?? "unknown",
            ServiceState: status is null ? "Unknown" : status.ServiceInstalled ? "Installed" : "Not installed",
            Reachability: status is null ? "Unknown" : status.Reachable ? "Reachable" : "Unreachable",
            Capability: ResolveCapability(status, realtimeState, realtimeAuthorization),
            ConnectionState: realtimeState.ToString(),
            OnboardingHealth: FormatOnboardingHealth(passed, warnings, failed),
            HealthState: healthState,
            DashboardUrl: status?.DashboardUrl ?? "unknown",
            LogPath: status?.LogPath ?? "unknown");
    }

    private static string ResolveCapability(
        GatewayStatusSnapshot? status,
        GatewayRealtimeState realtimeState,
        GatewayRealtimeAuthorization? realtimeAuthorization)
    {
        if (realtimeState == GatewayRealtimeState.Connected &&
            realtimeAuthorization is not null &&
            !string.Equals(realtimeAuthorization.Capability, "unknown", StringComparison.Ordinal))
        {
            return realtimeAuthorization.Capability;
        }

        return status?.Capability ?? "unknown";
    }

    private static string FormatOnboardingHealth(int passed, int warnings, int failed)
    {
        return $"{passed} passed, {warnings} {Pluralize("warning", warnings)}, {failed} failed";
    }

    private static string CreateHealthState(GatewayStatusSnapshot? status, int warnings, int failed)
    {
        if (status is null)
        {
            return "Checking";
        }

        if (failed > 0 || !status.Reachable)
        {
            return "Needs attention";
        }

        return warnings > 0 ? "Review" : "Healthy";
    }

    private static string Pluralize(string label, int count)
    {
        return count == 1 ? label : $"{label}s";
    }
}
