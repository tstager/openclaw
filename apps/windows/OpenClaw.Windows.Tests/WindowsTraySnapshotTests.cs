using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsTraySnapshotTests
{
    private static GatewayStatusSnapshot RunningStatus(bool serviceInstalled = true)
    {
        return new GatewayStatusSnapshot(
            State: "running",
            ServiceInstalled: serviceInstalled,
            Reachable: true,
            Capability: "admin_capable",
            DashboardUrl: "http://127.0.0.1:18080",
            LogPath: @"C:\openclaw\gateway.log",
            AuthWarning: null,
            Error: null,
            RawJson: "{}");
    }

    private static GatewayStatusSnapshot StatusWithDashboard(string? dashboardUrl)
    {
        return new GatewayStatusSnapshot(
            State: "running",
            ServiceInstalled: true,
            Reachable: true,
            Capability: "admin_capable",
            DashboardUrl: dashboardUrl,
            LogPath: null,
            AuthWarning: null,
            Error: null,
            RawJson: "{}");
    }

    private static WindowsTraySnapshot SnapshotForUrl(string? dashboardUrl)
    {
        return WindowsTraySnapshot.Create(
            StatusWithDashboard(dashboardUrl),
            GatewayRealtimeState.Connected,
            WindowsCanvasNodeState.Connected,
            canvasNodeEnabled: true,
            AppPreferences.Default,
            sessionCount: 0,
            pendingApprovalCount: 0,
            pendingPairingCount: 0,
            lastActivity: null,
            latestNotification: null);
    }

    private static GatewayStatusSnapshot StoppedStatus(bool serviceInstalled = true)
    {
        return new GatewayStatusSnapshot(
            State: "stopped",
            ServiceInstalled: serviceInstalled,
            Reachable: false,
            Capability: "unknown",
            DashboardUrl: null,
            LogPath: null,
            AuthWarning: null,
            Error: null,
            RawJson: "{}");
    }

    [TestMethod]
    public void CapturesRunningGatewayWithConnectedRealtimeAndReadyCanvas()
    {
        var snapshot = WindowsTraySnapshot.Create(
            RunningStatus(),
            GatewayRealtimeState.Connected,
            WindowsCanvasNodeState.Connected,
            canvasNodeEnabled: true,
            AppPreferences.Default,
            sessionCount: 3,
            pendingApprovalCount: 2,
            pendingPairingCount: 1,
            lastActivity: "Restart completed.",
            latestNotification: null);

        Assert.AreEqual("running", snapshot.GatewayState);
        Assert.AreEqual("http://127.0.0.1:18080", snapshot.GatewayUrl);
        Assert.IsTrue(snapshot.GatewayRunning);
        Assert.AreEqual("Connected", snapshot.ConnectionState);
        Assert.IsTrue(snapshot.RealtimeConnected);
        Assert.AreEqual(TrayRealtimeAction.Disconnect, snapshot.RealtimeAction);
        Assert.AreEqual(TrayCanvasReadiness.Ready, snapshot.CanvasReadiness);
        Assert.AreEqual(3, snapshot.SessionCount);
        Assert.AreEqual(2, snapshot.PendingApprovalCount);
        Assert.AreEqual(1, snapshot.PendingPairingCount);
        Assert.AreEqual("Restart completed.", snapshot.LatestActivity);
    }

    [TestMethod]
    public void StoppedDisconnectedGatewayOffersStartAndConnect()
    {
        var snapshot = WindowsTraySnapshot.Create(
            StoppedStatus(),
            GatewayRealtimeState.Disconnected,
            WindowsCanvasNodeState.Disconnected,
            canvasNodeEnabled: true,
            AppPreferences.Default,
            sessionCount: 0,
            pendingApprovalCount: 0,
            pendingPairingCount: 0,
            lastActivity: null,
            latestNotification: null);

        Assert.IsFalse(snapshot.GatewayRunning);
        Assert.IsFalse(snapshot.RealtimeConnected);
        Assert.AreEqual(TrayRealtimeAction.Connect, snapshot.RealtimeAction);
        Assert.AreEqual(TrayCanvasReadiness.Disconnected, snapshot.CanvasReadiness);
        CollectionAssert.AreEqual(
            new[] { GatewayCliAction.Start },
            snapshot.AvailableGatewayActions.ToArray());
    }

    [TestMethod]
    public void MissingStatusReportsUnknownAndNoGatewayActions()
    {
        var snapshot = WindowsTraySnapshot.Create(
            null,
            GatewayRealtimeState.Disconnected,
            WindowsCanvasNodeState.Disconnected,
            canvasNodeEnabled: true,
            AppPreferences.Default,
            sessionCount: 0,
            pendingApprovalCount: 0,
            pendingPairingCount: 0,
            lastActivity: null,
            latestNotification: null);

        Assert.AreEqual("unknown", snapshot.GatewayState);
        Assert.AreEqual(AppPreferences.Default.GatewayUrl, snapshot.GatewayUrl);
        Assert.IsFalse(snapshot.GatewayRunning);
        Assert.IsEmpty(snapshot.AvailableGatewayActions);
    }

    [TestMethod]
    public void RunningGatewayOffersRestartAndStop()
    {
        var snapshot = WindowsTraySnapshot.Create(
            RunningStatus(),
            GatewayRealtimeState.Connected,
            WindowsCanvasNodeState.Connected,
            canvasNodeEnabled: true,
            AppPreferences.Default,
            sessionCount: 0,
            pendingApprovalCount: 0,
            pendingPairingCount: 0,
            lastActivity: null,
            latestNotification: null);

        CollectionAssert.AreEqual(
            new[] { GatewayCliAction.Restart, GatewayCliAction.Stop },
            snapshot.AvailableGatewayActions.ToArray());
    }

    [TestMethod]
    public void UninstalledStoppedGatewayOffersInstallAndStart()
    {
        var snapshot = WindowsTraySnapshot.Create(
            StoppedStatus(serviceInstalled: false),
            GatewayRealtimeState.Disconnected,
            WindowsCanvasNodeState.Disconnected,
            canvasNodeEnabled: true,
            AppPreferences.Default,
            sessionCount: 0,
            pendingApprovalCount: 0,
            pendingPairingCount: 0,
            lastActivity: null,
            latestNotification: null);

        CollectionAssert.AreEqual(
            new[] { GatewayCliAction.Install, GatewayCliAction.Start },
            snapshot.AvailableGatewayActions.ToArray());
    }

    [TestMethod]
    public void DisabledCanvasNodeReportsDisabledRegardlessOfState()
    {
        var snapshot = WindowsTraySnapshot.Create(
            RunningStatus(),
            GatewayRealtimeState.Connected,
            WindowsCanvasNodeState.Connected,
            canvasNodeEnabled: false,
            AppPreferences.Default,
            sessionCount: 0,
            pendingApprovalCount: 0,
            pendingPairingCount: 0,
            lastActivity: null,
            latestNotification: null);

        Assert.AreEqual(TrayCanvasReadiness.Disabled, snapshot.CanvasReadiness);
    }

    [TestMethod]
    public void CanvasPairingRequiredStateIsSurfaced()
    {
        var snapshot = WindowsTraySnapshot.Create(
            RunningStatus(),
            GatewayRealtimeState.Connected,
            WindowsCanvasNodeState.PairingRequired,
            canvasNodeEnabled: true,
            AppPreferences.Default,
            sessionCount: 0,
            pendingApprovalCount: 0,
            pendingPairingCount: 0,
            lastActivity: null,
            latestNotification: null);

        Assert.AreEqual(TrayCanvasReadiness.PairingRequired, snapshot.CanvasReadiness);
    }

    [TestMethod]
    public void FallsBackToLatestNotificationWhenNoActivityText()
    {
        var notification = new WindowsNotificationActivity(
            DateTimeOffset.Now,
            WindowsNavigationDestination.Approvals,
            "OpenClaw approval",
            "2 approval requests pending.");

        var snapshot = WindowsTraySnapshot.Create(
            RunningStatus(),
            GatewayRealtimeState.Connected,
            WindowsCanvasNodeState.Connected,
            canvasNodeEnabled: true,
            AppPreferences.Default,
            sessionCount: 0,
            pendingApprovalCount: 2,
            pendingPairingCount: 0,
            lastActivity: null,
            latestNotification: notification);

        Assert.AreEqual("OpenClaw approval: 2 approval requests pending.", snapshot.LatestActivity);
    }

    [TestMethod]
    public void NegativeCountsAreClampedToZero()
    {
        var snapshot = WindowsTraySnapshot.Create(
            RunningStatus(),
            GatewayRealtimeState.Connected,
            WindowsCanvasNodeState.Connected,
            canvasNodeEnabled: true,
            AppPreferences.Default,
            sessionCount: -5,
            pendingApprovalCount: -1,
            pendingPairingCount: -3,
            lastActivity: null,
            latestNotification: null);

        Assert.AreEqual(0, snapshot.SessionCount);
        Assert.AreEqual(0, snapshot.PendingApprovalCount);
        Assert.AreEqual(0, snapshot.PendingPairingCount);
    }

    [TestMethod]
    public void LoopbackDashboardUrlIsLocal()
    {
        Assert.IsTrue(SnapshotForUrl("http://127.0.0.1:18080").GatewayIsLocal);
        Assert.IsTrue(SnapshotForUrl("http://localhost:18080").GatewayIsLocal);
        Assert.IsTrue(SnapshotForUrl("http://[::1]:18080").GatewayIsLocal);
    }

    [TestMethod]
    public void RemoteHostDashboardUrlIsNotLocal()
    {
        Assert.IsFalse(SnapshotForUrl("https://gateway.example.com:18080").GatewayIsLocal);
        Assert.IsFalse(SnapshotForUrl("http://10.0.0.5:18080").GatewayIsLocal);
    }

    [TestMethod]
    public void MissingDashboardUrlFallsBackToPreferenceHostForLocality()
    {
        var snapshot = SnapshotForUrl(null);

        Assert.AreEqual(AppPreferences.Default.GatewayUrl, snapshot.GatewayUrl);
        var expectedLocal =
            AppPreferences.Default.GatewayUrl.Contains("127.0.0.1", StringComparison.Ordinal) ||
            AppPreferences.Default.GatewayUrl.Contains("localhost", StringComparison.Ordinal);
        Assert.AreEqual(expectedLocal, snapshot.GatewayIsLocal);
    }

    [TestMethod]
    public void CarriesThemeAccentAndColorThemePreferences()
    {
        var preferences = AppPreferences.Default with
        {
            ThemePreference = WindowsThemePreference.Dark,
            AccentColorPreference = WindowsAccentColorPreference.Teal,
            ColorThemePreference = WindowsColorThemePreference.Ocean,
        };

        var snapshot = WindowsTraySnapshot.Create(
            RunningStatus(),
            GatewayRealtimeState.Connected,
            WindowsCanvasNodeState.Connected,
            canvasNodeEnabled: true,
            preferences,
            sessionCount: 0,
            pendingApprovalCount: 0,
            pendingPairingCount: 0,
            lastActivity: null,
            latestNotification: null);

        Assert.AreEqual(WindowsThemePreference.Dark, snapshot.Theme);
        Assert.AreEqual(WindowsAccentColorPreference.Teal, snapshot.Accent);
        Assert.AreEqual(WindowsColorThemePreference.Ocean, snapshot.ColorTheme);
        Assert.IsTrue(snapshot.NotificationsEnabled);
    }
}
