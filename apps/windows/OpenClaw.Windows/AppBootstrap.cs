using OpenClaw.Protocol.Generated;
using OpenClaw.Windows.Native;

namespace OpenClaw.Windows;

public sealed record AppStartupSummary(
    string AppName,
    int GatewayProtocolVersion,
    WindowsHostCapabilities HostCapabilities);

public sealed record WindowsCompanionState(
    AppStartupSummary Summary,
    GatewayCompanionController Gateway,
    GatewayRealtimeClient Realtime,
    WindowsDeviceCapabilityService DeviceCapabilities,
    OnboardingCheckService OnboardingChecks,
    AppPreferencesStore Preferences,
    WindowsNavigationService Navigation,
    WindowsNotificationActivityLog Notifications);

public static class AppBootstrap
{
    public static AppStartupSummary CreateStartupSummary()
    {
        return new AppStartupSummary(
            AppName: "OpenClaw",
            GatewayProtocolVersion: GatewayProtocol.Version,
            HostCapabilities: WindowsHostCapabilityProbe.Current);
    }

    public static WindowsCompanionState CreateAppState()
    {
        var credentials = new PasswordVaultAppCredentialStore();
        var preferences = AppPreferencesStore.CreateDefault(credentials);
        var commandRunner = GatewayCliCommandRunner.CreateDefault();
        var gateway = new GatewayCompanionController(commandRunner, preferences);
        return new WindowsCompanionState(
            Summary: CreateStartupSummary(),
            Gateway: gateway,
            Realtime: new GatewayRealtimeClient(preferences, new DeviceIdentityStore(credentials)),
            DeviceCapabilities: new WindowsDeviceCapabilityService(),
            OnboardingChecks: new OnboardingCheckService(commandRunner),
            Preferences: preferences,
            Navigation: new WindowsNavigationService(),
            Notifications: new WindowsNotificationActivityLog());
    }
}
