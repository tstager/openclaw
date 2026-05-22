using OpenClaw.Windows;
using OpenClaw.Windows.Native;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsGuidedOnboardingServiceTests
{
    [TestMethod]
    public void CreatePlan_PrioritizesGatewayInstallAndSettingsRepair()
    {
        var service = new WindowsGuidedOnboardingService();

        var plan = service.CreatePlan(
            AppPreferences.Default,
            gatewayStatus: null,
            realtimeState: GatewayRealtimeState.Disconnected,
            tunnelStatus: new WindowsSshTunnelStatus(false, "Stopped", null),
            onboardingChecks:
            [
                new OnboardingCheckResult("openclaw", "OpenClaw CLI", OnboardingCheckState.Passed, "ok"),
                new OnboardingCheckResult("node", "Node runtime", OnboardingCheckState.Passed, "ok"),
            ],
            browserProxyStatus: new WindowsBrowserProxyStatus(
                "Misconfigured",
                "The saved gateway URL is invalid.",
                "Save a valid gateway URL before wiring browser proxy commands.",
                null),
            textToSpeechStatus: new WindowsTextToSpeechStatus("Unavailable", "No voices installed.", null, 0));

        CollectionAssert.AreEqual(
            new[]
            {
                WindowsGuidedActionKey.InstallGateway,
                WindowsGuidedActionKey.OpenSettings,
                WindowsGuidedActionKey.OpenDevices,
            },
            plan.Actions.Select(action => action.Key).ToArray());
    }

    [TestMethod]
    public void CreatePlan_WhenHealthySuggestsFeatureValidation()
    {
        var service = new WindowsGuidedOnboardingService();

        var plan = service.CreatePlan(
            AppPreferences.Default,
            gatewayStatus: GatewayStatusSnapshot.FromJson(
                """{"ok":true,"service":{"installed":true,"state":"running"},"rpc":{"ok":true,"capability":"paired"},"dashboard":{"url":"http://127.0.0.1:18789"}}"""),
            realtimeState: GatewayRealtimeState.Connected,
            tunnelStatus: new WindowsSshTunnelStatus(false, "Stopped", null),
            onboardingChecks:
            [
                new OnboardingCheckResult("openclaw", "OpenClaw CLI", OnboardingCheckState.Passed, "ok"),
                new OnboardingCheckResult("node", "Node runtime", OnboardingCheckState.Passed, "ok"),
                new OnboardingCheckResult("gateway", "Gateway status", OnboardingCheckState.Passed, "ok"),
            ],
            browserProxyStatus: new WindowsBrowserProxyStatus(
                "Ready for shell wiring",
                "Browser proxy is ready.",
                "No repair needed.",
                "http://127.0.0.1:18789"),
            textToSpeechStatus: new WindowsTextToSpeechStatus("Available", "Voices installed.", "Default", 2));

        CollectionAssert.AreEqual(
            new[]
            {
                WindowsGuidedActionKey.OpenDevices,
                WindowsGuidedActionKey.OpenLogs,
            },
            plan.Actions.Select(action => action.Key).ToArray());
        StringAssert.Contains(plan.Summary, "healthy");
    }
}
