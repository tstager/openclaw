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
public sealed record WindowsCompanionState(
    AppStartupSummary Summary,
    GatewayCompanionController Gateway,
    GatewayRealtimeClient Realtime,
    WindowsCanvasNodeClient CanvasNode,
    WindowsDeviceCapabilityService DeviceCapabilities,
    OnboardingCheckService OnboardingChecks,
    AppPreferencesStore Preferences,
    WindowsNavigationService Navigation,
    WindowsNotificationActivityLog Notifications);

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
        var credentials = new PasswordVaultAppCredentialStore();
        var preferences = AppPreferencesStore.CreateDefault(credentials);
        var deviceIdentityStore = new DeviceIdentityStore(credentials);
        var commandRunner = GatewayCliCommandRunner.CreateDefault();
        var gateway = new GatewayCompanionController(commandRunner, preferences);
        return new WindowsCompanionState(
            Summary: CreateStartupSummary(),
            Gateway: gateway,
            Realtime: new GatewayRealtimeClient(preferences, deviceIdentityStore),
            CanvasNode: new WindowsCanvasNodeClient(preferences, deviceIdentityStore),
            DeviceCapabilities: new WindowsDeviceCapabilityService(),
            OnboardingChecks: new OnboardingCheckService(commandRunner, preferences),
            Preferences: preferences,
            Navigation: new WindowsNavigationService(),
            Notifications: new WindowsNotificationActivityLog());
    }
}
