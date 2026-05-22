using OpenClaw.Protocol.Generated;
using OpenClaw.Windows.Native;

namespace OpenClaw.Windows;

/// <summary>
/// Immutable facts shown in the shell header and onboarding diagnostics.
/// </summary>
public sealed record AppStartupSummary(
    string AppName,
    int GatewayProtocolVersion,
    WindowsHostCapabilities HostCapabilities);

/// <summary>
/// The dependency graph shared by the WinUI shell, tray host, and workflow coordinators.
/// </summary>
public sealed record WindowsCompanionState
{
    public required AppStartupSummary Summary { get; init; }

    public required GatewayCompanionController Gateway { get; init; }

    public required GatewayRealtimeClient Realtime { get; init; }

    public required WindowsCanvasNodeClient CanvasNode { get; init; }

    public required WindowsDeviceCapabilityService DeviceCapabilities { get; init; }

    public required OnboardingCheckService OnboardingChecks { get; init; }

    public required AppPreferencesStore Preferences { get; init; }

    public required WindowsNavigationService Navigation { get; init; }

    public required WindowsNotificationActivityLog Notifications { get; init; }

    public required WindowsActivationRelay Activation { get; init; }

    public required WindowsSshTunnelService Tunnel { get; init; }

    public required WindowsPortTopologyService Topology { get; init; }

    public required WindowsStructuredDiagnosticsWriter Diagnostics { get; init; }

    public required WindowsActivityHistoryStore ActivityHistory { get; init; }

    public required WindowsUrlRiskEvaluator UrlRisk { get; init; }

    public required WindowsSecretRedactor SecretRedactor { get; init; }
}

/// <summary>
/// Creates production Windows companion services with their storage, credential, and gateway dependencies.
/// </summary>
public static class AppBootstrap
{
    /// <summary>
    /// Collects product, protocol, and native host capability metadata for display and diagnostics.
    /// </summary>
    public static AppStartupSummary CreateStartupSummary()
    {
        return new AppStartupSummary(
            AppName: "OpenClaw",
            GatewayProtocolVersion: GatewayProtocol.Version,
            HostCapabilities: WindowsHostCapabilityProbe.Current);
    }

    /// <summary>
    /// Builds the app's long-lived service graph. Tests construct this graph manually with fakes.
    /// </summary>
    public static WindowsCompanionState CreateAppState()
    {
        var companionRoot = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "WindowsCompanion");
        var credentials = new PasswordVaultAppCredentialStore();
        var preferences = AppPreferencesStore.CreateDefault(credentials);
        var deviceIdentityStore = new DeviceIdentityStore(credentials);
        var commandRunner = GatewayCliCommandRunner.CreateDefault();
        var gateway = new GatewayCompanionController(commandRunner, preferences);
        return new WindowsCompanionState
        {
            Summary = CreateStartupSummary(),
            Gateway = gateway,
            Realtime = new GatewayRealtimeClient(preferences, deviceIdentityStore),
            CanvasNode = new WindowsCanvasNodeClient(preferences, deviceIdentityStore),
            DeviceCapabilities = new WindowsDeviceCapabilityService(),
            OnboardingChecks = new OnboardingCheckService(commandRunner, preferences),
            Preferences = preferences,
            Navigation = new WindowsNavigationService(),
            Notifications = new WindowsNotificationActivityLog(),
            Activation = new WindowsActivationRelay("OpenClaw.Windows.Companion.Activation"),
            Tunnel = new WindowsSshTunnelService(),
            Topology = new WindowsPortTopologyService(),
            Diagnostics = new WindowsStructuredDiagnosticsWriter(System.IO.Path.Combine(companionRoot, "activity-diagnostics.jsonl")),
            ActivityHistory = new WindowsActivityHistoryStore(System.IO.Path.Combine(companionRoot, "activity-history.json")),
            UrlRisk = new WindowsUrlRiskEvaluator(),
            SecretRedactor = new WindowsSecretRedactor(),
        };
    }
}
