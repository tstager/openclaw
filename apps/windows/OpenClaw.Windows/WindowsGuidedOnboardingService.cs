using OpenClaw.Windows.Native;

namespace OpenClaw.Windows;

/// <summary>
/// Stable action identifiers used by the shell's guided onboarding surface.
/// </summary>
public static class WindowsGuidedActionKey
{
    public const string InstallGateway = "install-gateway";
    public const string StartGateway = "start-gateway";
    public const string ConnectGateway = "connect-gateway";
    public const string StartTunnel = "start-tunnel";
    public const string OpenSettings = "open-settings";
    public const string OpenDevices = "open-devices";
    public const string OpenLogs = "open-logs";
}

/// <summary>
/// One guided next-step action shown on the Home page.
/// </summary>
public sealed record WindowsGuidedAction(
    string Key,
    string Title,
    string Detail,
    string Destination);

/// <summary>
/// Compact guided setup plan derived from live gateway, topology, and capability state.
/// </summary>
public sealed record WindowsGuidedOnboardingPlan(
    string Summary,
    IReadOnlyList<WindowsGuidedAction> Actions);

/// <summary>
/// Converts diagnostics-only onboarding state into focused next-step actions.
/// </summary>
public sealed class WindowsGuidedOnboardingService
{
    private readonly IWindowsStringLocalizer localizer;

    public WindowsGuidedOnboardingService(IWindowsStringLocalizer? localizer = null)
    {
        this.localizer = localizer ?? new WindowsStringLocalizer();
    }

    public WindowsGuidedOnboardingPlan CreatePlan(
        AppPreferences preferences,
        GatewayStatusSnapshot? gatewayStatus,
        GatewayRealtimeState realtimeState,
        WindowsSshTunnelStatus tunnelStatus,
        IReadOnlyList<OnboardingCheckResult> onboardingChecks,
        WindowsBrowserProxyStatus browserProxyStatus,
        WindowsTextToSpeechStatus textToSpeechStatus)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(tunnelStatus);
        ArgumentNullException.ThrowIfNull(onboardingChecks);
        ArgumentNullException.ThrowIfNull(browserProxyStatus);
        ArgumentNullException.ThrowIfNull(textToSpeechStatus);

        List<WindowsGuidedAction> actions = [];
        var failedChecks = onboardingChecks.Where(check => check.State == OnboardingCheckState.Failed).ToArray();
        var warningChecks = onboardingChecks.Where(check => check.State == OnboardingCheckState.Warning).ToArray();

        if (gatewayStatus is null || !gatewayStatus.ServiceInstalled)
        {
            actions.Add(new WindowsGuidedAction(
                WindowsGuidedActionKey.InstallGateway,
                this.localizer.Get("Shell.Home.GuidedActions.InstallGateway.Title", "Install the gateway"),
                this.localizer.Get(
                    "Shell.Home.GuidedActions.InstallGateway.Detail",
                    "The local gateway service is not installed yet. Install it before pairing or browser routing."),
                WindowsNavigationDestination.Home));
        }
        else if (!gatewayStatus.Reachable)
        {
            actions.Add(new WindowsGuidedAction(
                WindowsGuidedActionKey.StartGateway,
                this.localizer.Get("Shell.Home.GuidedActions.StartGateway.Title", "Start the gateway"),
                this.localizer.Get(
                    "Shell.Home.GuidedActions.StartGateway.Detail",
                    "The gateway service is installed but not reachable from the Windows companion."),
                WindowsNavigationDestination.Home));
        }

        if ((preferences.Topology.AutoStartTunnel || !string.IsNullOrWhiteSpace(preferences.Topology.SshHost)) &&
            !tunnelStatus.Running)
        {
            actions.Add(new WindowsGuidedAction(
                WindowsGuidedActionKey.StartTunnel,
                this.localizer.Get("Shell.Home.GuidedActions.StartTunnel.Title", "Start the SSH tunnel"),
                this.localizer.Get(
                    "Shell.Home.GuidedActions.StartTunnel.Detail",
                    "Saved topology settings expect a local tunnel, but the forwarded listener is not running yet."),
                WindowsNavigationDestination.Settings));
        }

        if (gatewayStatus?.Reachable == true && realtimeState != GatewayRealtimeState.Connected)
        {
            actions.Add(new WindowsGuidedAction(
                WindowsGuidedActionKey.ConnectGateway,
                this.localizer.Get("Shell.Home.GuidedActions.ConnectGateway.Title", "Connect realtime"),
                this.localizer.Get(
                    "Shell.Home.GuidedActions.ConnectGateway.Detail",
                    "The gateway is reachable. Connect the realtime channel to unlock chat, approvals, and pairing updates."),
                WindowsNavigationDestination.Home));
        }

        if (browserProxyStatus.State is "Misconfigured" or "Gateway unavailable")
        {
            actions.Add(new WindowsGuidedAction(
                WindowsGuidedActionKey.OpenSettings,
                this.localizer.Get("Shell.Home.GuidedActions.BrowserProxy.Title", "Review browser proxy routing"),
                browserProxyStatus.RepairGuidance,
                WindowsNavigationDestination.Settings));
        }

        if (textToSpeechStatus.InstalledVoiceCount == 0)
        {
            actions.Add(new WindowsGuidedAction(
                WindowsGuidedActionKey.OpenDevices,
                this.localizer.Get("Shell.Home.GuidedActions.SystemSpeech.Title", "Check Windows speech voices"),
                this.localizer.Get(
                    "Shell.Home.GuidedActions.SystemSpeech.Detail",
                    "Install a Windows voice package so the companion can save local speech clips."),
                WindowsNavigationDestination.Devices));
        }

        if (actions.Count == 0 && failedChecks.Length > 0)
        {
            actions.Add(new WindowsGuidedAction(
                WindowsGuidedActionKey.OpenLogs,
                this.localizer.Get("Shell.Home.GuidedActions.Failures.Title", "Review onboarding failures"),
                this.localizer.Format(
                    "Shell.Home.GuidedActions.Failures.Detail",
                    "{0} prerequisite check(s) failed. Review diagnostics and local setup details.",
                    failedChecks.Length),
                WindowsNavigationDestination.Logs));
        }

        if (actions.Count == 0)
        {
            actions.Add(new WindowsGuidedAction(
                WindowsGuidedActionKey.OpenDevices,
                this.localizer.Get("Shell.Home.GuidedActions.ValidateFeatures.Title", "Review runtime features"),
                this.localizer.Get(
                    "Shell.Home.GuidedActions.ValidateFeatures.Detail",
                    "Gateway, realtime, and core capabilities look ready. Validate devices, notifications, and speech output."),
                WindowsNavigationDestination.Devices));
            actions.Add(new WindowsGuidedAction(
                WindowsGuidedActionKey.OpenLogs,
                this.localizer.Get("Shell.Home.GuidedActions.SupportSnapshot.Title", "Capture a support snapshot"),
                this.localizer.Get(
                    "Shell.Home.GuidedActions.SupportSnapshot.Detail",
                    "Copy or save an operational support summary after major setup changes."),
                WindowsNavigationDestination.Logs));
        }

        return new WindowsGuidedOnboardingPlan(
            Summary: this.CreateSummary(actions, warningChecks.Length, failedChecks.Length, realtimeState),
            Actions: actions.Take(3).ToArray());
    }

    private string CreateSummary(
        IReadOnlyCollection<WindowsGuidedAction> actions,
        int warningCount,
        int failedCount,
        GatewayRealtimeState realtimeState)
    {
        if (failedCount > 0)
        {
            return this.localizer.Format(
                "Shell.Home.GuidedActions.Summary.Failures",
                "{0} prerequisite check(s) failed. Complete the next guided step before continuing.",
                failedCount);
        }

        if (actions.Any(action => action.Key == WindowsGuidedActionKey.ConnectGateway))
        {
            return this.localizer.Get(
                "Shell.Home.GuidedActions.Summary.ConnectRealtime",
                "The local gateway is ready. Connect realtime next to unlock live operator workflows.");
        }

        if (warningCount > 0)
        {
            return this.localizer.Format(
                "Shell.Home.GuidedActions.Summary.Warnings",
                "{0} setup warning(s) remain. Follow the next recommended action to tighten runtime readiness.",
                warningCount);
        }

        if (realtimeState == GatewayRealtimeState.Connected)
        {
            return this.localizer.Get(
                "Shell.Home.GuidedActions.Summary.Healthy",
                "Windows companion runtime checks look healthy. Use the actions below to validate feature depth.");
        }

        return this.localizer.Get(
            "Shell.Home.GuidedActions.Summary.Default",
            "Use the next guided action to finish Windows companion setup.");
    }
}
