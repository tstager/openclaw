using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class TrayFlyoutComposerTests
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

    private static WindowsTraySnapshot Snapshot(
        GatewayStatusSnapshot? status,
        GatewayRealtimeState realtime,
        WindowsCanvasNodeState canvas,
        bool canvasEnabled = true,
        int approvals = 0,
        int pairings = 0,
        int sessions = 0,
        string? lastActivity = null,
        AppPreferences? preferences = null)
    {
        return WindowsTraySnapshot.Create(
            status,
            realtime,
            canvas,
            canvasEnabled,
            preferences ?? AppPreferences.Default,
            sessionCount: sessions,
            pendingApprovalCount: approvals,
            pendingPairingCount: pairings,
            lastActivity: lastActivity,
            latestNotification: null);
    }

    private static IEnumerable<TrayToggleRow> AllToggleRows(TrayFlyoutModel model)
    {
        return model.Sections.SelectMany(section => section.ToggleRows);
    }

    private static IEnumerable<TrayActionRow> AllActionRows(TrayFlyoutModel model)
    {
        return model.Sections.SelectMany(section => section.ActionRows);
    }

    private static IEnumerable<TrayStatusRow> AllStatusRows(TrayFlyoutModel model)
    {
        return model.Sections.SelectMany(section => section.StatusRows);
    }

    [TestMethod]
    public void ComposesGatewayCanvasStatusRowsAndCoreActions()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var statusLabels = AllStatusRows(model).Select(row => row.Label).ToArray();
        Assert.IsTrue(statusLabels.Any(label => label.StartsWith("Gateway:", StringComparison.Ordinal)));
        Assert.IsTrue(statusLabels.Any(label => label.StartsWith("Canvas:", StringComparison.Ordinal)));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.Contains(actions, TrayFlyoutAction.OpenShell);
        CollectionAssert.Contains(actions, TrayFlyoutAction.OpenSettings);
        CollectionAssert.Contains(actions, TrayFlyoutAction.OpenLogs);
        CollectionAssert.Contains(actions, TrayFlyoutAction.Exit);
    }

    [TestMethod]
    public void ExitIsAlwaysTheLastAction()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var actions = AllActionRows(model).ToArray();
        Assert.AreEqual(TrayFlyoutAction.Exit, actions[^1].Action);
    }

    [TestMethod]
    public void RunningConnectedGatewayDotIsSuccess()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var gatewayRow = AllStatusRows(model).First(row => row.Label.StartsWith("Gateway:", StringComparison.Ordinal));
        Assert.AreEqual(TrayStatusTone.Success, gatewayRow.Tone);
    }

    [TestMethod]
    public void RunningButDisconnectedGatewayDotIsCaution()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var gatewayRow = AllStatusRows(model).First(row => row.Label.StartsWith("Gateway:", StringComparison.Ordinal));
        Assert.AreEqual(TrayStatusTone.Caution, gatewayRow.Tone);
    }

    [TestMethod]
    public void StoppedGatewayDotIsCritical()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var gatewayRow = AllStatusRows(model).First(row => row.Label.StartsWith("Gateway:", StringComparison.Ordinal));
        Assert.AreEqual(TrayStatusTone.Critical, gatewayRow.Tone);
    }

    [TestMethod]
    public void ReadyCanvasDotIsSuccessAndDisabledIsNeutral()
    {
        var ready = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));
        var disabled = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, canvasEnabled: false));

        Assert.AreEqual(
            TrayStatusTone.Success,
            AllStatusRows(ready).First(row => row.Label.StartsWith("Canvas:", StringComparison.Ordinal)).Tone);
        Assert.AreEqual(
            TrayStatusTone.Neutral,
            AllStatusRows(disabled).First(row => row.Label.StartsWith("Canvas:", StringComparison.Ordinal)).Tone);
    }

    [TestMethod]
    public void DisconnectedRealtimeOffersConnectAction()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.Contains(actions, TrayFlyoutAction.ConnectRealtime);
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.DisconnectRealtime);
    }

    [TestMethod]
    public void ConnectedRealtimeOffersDisconnectAction()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.Contains(actions, TrayFlyoutAction.DisconnectRealtime);
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.ConnectRealtime);
    }

    [TestMethod]
    public void ConnectingRealtimeOffersNoRealtimeToggle()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connecting, WindowsCanvasNodeState.Connecting));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.ConnectRealtime);
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.DisconnectRealtime);
    }

    [TestMethod]
    public void RunningGatewayExposesRestartAndStopRows()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.Contains(actions, TrayFlyoutAction.RunGatewayRestart);
        CollectionAssert.Contains(actions, TrayFlyoutAction.RunGatewayStop);
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.RunGatewayStart);
    }

    [TestMethod]
    public void UninstalledStoppedGatewayExposesInstallAndStartRows()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(serviceInstalled: false), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.Contains(actions, TrayFlyoutAction.RunGatewayInstall);
        CollectionAssert.Contains(actions, TrayFlyoutAction.RunGatewayStart);
    }

    [TestMethod]
    public void OpenShellRowNeverCarriesAPendingBadge()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, approvals: 2, pairings: 1));

        var openRow = AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenShell);
        Assert.IsNull(openRow.Badge);
    }

    [TestMethod]
    public void NavigationQuickActionsArePresentWithGlyphsAndActions()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var byAction = AllActionRows(model).ToDictionary(row => row.Action);
        foreach (var action in new[]
        {
            TrayFlyoutAction.OpenHome,
            TrayFlyoutAction.OpenChat,
            TrayFlyoutAction.OpenCanvas,
            TrayFlyoutAction.OpenSessions,
            TrayFlyoutAction.OpenApprovals,
            TrayFlyoutAction.OpenPairing,
            TrayFlyoutAction.OpenSettings,
            TrayFlyoutAction.OpenLogs,
        })
        {
            Assert.IsTrue(byAction.ContainsKey(action), $"Missing quick-action row {action}.");
            Assert.IsFalse(string.IsNullOrEmpty(byAction[action].Glyph), $"Action {action} is missing a glyph.");
        }
    }

    [TestMethod]
    public void ApprovalsAndPairingRowsCarryTheirOwnCountBadges()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, approvals: 2, pairings: 1));

        var approvalsRow = AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenApprovals);
        var pairingRow = AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenPairing);
        Assert.AreEqual("2", approvalsRow.Badge);
        Assert.AreEqual("1", pairingRow.Badge);
    }

    [TestMethod]
    public void SessionsRowBadgesTheCountAndIsAbsentAtZero()
    {
        var withSessions = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, sessions: 5));
        var sessionsRow = AllActionRows(withSessions).First(row => row.Action == TrayFlyoutAction.OpenSessions);
        Assert.AreEqual("5", sessionsRow.Badge);

        var withoutSessions = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, sessions: 0));
        var zeroRow = AllActionRows(withoutSessions).First(row => row.Action == TrayFlyoutAction.OpenSessions);
        Assert.IsNull(zeroRow.Badge);
    }

    [TestMethod]
    public void ZeroPendingWorkLeavesApprovalsAndPairingRowsWithoutBadges()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        Assert.IsNull(AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenApprovals).Badge);
        Assert.IsNull(AllActionRows(model).First(row => row.Action == TrayFlyoutAction.OpenPairing).Badge);
    }

    [TestMethod]
    public void QuickActionsFollowTheExpectedNavigationOrdering()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(serviceInstalled: false), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.Disconnected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        var expectedNav = new[]
        {
            TrayFlyoutAction.OpenShell,
            TrayFlyoutAction.OpenHome,
            TrayFlyoutAction.OpenChat,
            TrayFlyoutAction.OpenCanvas,
            TrayFlyoutAction.OpenSessions,
            TrayFlyoutAction.OpenApprovals,
            TrayFlyoutAction.OpenPairing,
            TrayFlyoutAction.OpenSettings,
            TrayFlyoutAction.OpenLogs,
        };
        CollectionAssert.AreEqual(expectedNav, actions.Take(expectedNav.Length).ToArray());
        Assert.AreEqual(TrayFlyoutAction.Exit, actions[^1]);
    }

    [TestMethod]
    public void LocalRunningGatewayRowCarriesLocalBadge()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var gatewayRow = AllStatusRows(model).First(row => row.Label.StartsWith("Gateway:", StringComparison.Ordinal));
        Assert.AreEqual("Local", gatewayRow.Badge);
    }

    [TestMethod]
    public void RemoteGatewayRowCarriesRemoteBadge()
    {
        var remoteStatus = new GatewayStatusSnapshot(
            State: "running",
            ServiceInstalled: true,
            Reachable: true,
            Capability: "admin_capable",
            DashboardUrl: "https://gateway.example.com:18080",
            LogPath: null,
            AuthWarning: null,
            Error: null,
            RawJson: "{}");
        var model = TrayFlyoutComposer.Compose(
            Snapshot(remoteStatus, GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var gatewayRow = AllStatusRows(model).First(row => row.Label.StartsWith("Gateway:", StringComparison.Ordinal));
        Assert.AreEqual("Remote", gatewayRow.Badge);
    }

    [TestMethod]
    public void KnownSessionsAddAnAccentSessionsRowWithBadge()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, sessions: 4));

        var sessionRow = AllStatusRows(model).First(row => row.Label.StartsWith("Sessions:", StringComparison.Ordinal));
        Assert.AreEqual(TrayStatusTone.Accent, sessionRow.Tone);
        Assert.AreEqual("4", sessionRow.Badge);
    }

    [TestMethod]
    public void ZeroSessionsOmitsTheSessionsRow()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, sessions: 0));

        Assert.IsFalse(AllStatusRows(model).Any(row => row.Label.StartsWith("Sessions:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PresentActivityAddsAnActivityRow()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, lastActivity: "Restart completed."));

        var activityRow = AllStatusRows(model).First(row => row.Label == "Activity");
        Assert.AreEqual("Restart completed.", activityRow.Detail);
    }

    [TestMethod]
    public void AbsentActivityOmitsTheActivityRow()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, lastActivity: null));

        Assert.IsFalse(AllStatusRows(model).Any(row => row.Label == "Activity"));
    }

    [TestMethod]
    public void TooltipIncludesGatewayCanvasLocalityAndStaysWithinLimit()
    {
        var tooltip = TrayFlyoutComposer.BuildTooltip(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected),
            warningCount: 0);

        StringAssert.Contains(tooltip, "Running");
        StringAssert.Contains(tooltip, "Local");
        StringAssert.Contains(tooltip, "Canvas Ready");
        Assert.IsLessThanOrEqualTo(TrayFlyoutComposer.TooltipMaxLength, tooltip.Length);
    }

    [TestMethod]
    public void TooltipIncludesWarningCountWhenPresent()
    {
        var tooltip = TrayFlyoutComposer.BuildTooltip(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected),
            warningCount: 3);

        StringAssert.Contains(tooltip, "3 warnings");
        Assert.IsLessThanOrEqualTo(TrayFlyoutComposer.TooltipMaxLength, tooltip.Length);
    }

    [TestMethod]
    public void TooltipTruncatesLongActivityToStayWithinLimit()
    {
        var tooltip = TrayFlyoutComposer.BuildTooltip(
            Snapshot(
                RunningStatus(),
                GatewayRealtimeState.Connected,
                WindowsCanvasNodeState.Connected,
                lastActivity: new string('x', 200)),
            warningCount: 0);

        Assert.AreEqual(TrayFlyoutComposer.TooltipMaxLength, tooltip.Length);
    }

    [TestMethod]
    public void EveryActionRowCarriesAGlyph()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(StoppedStatus(serviceInstalled: false), GatewayRealtimeState.Disconnected, WindowsCanvasNodeState.PairingRequired));

        foreach (var row in AllActionRows(model))
        {
            Assert.IsFalse(string.IsNullOrEmpty(row.Glyph), $"Action {row.Action} is missing a glyph.");
        }
    }

    [TestMethod]
    public void PermissionsSectionExposesEveryLocalCapabilityToggle()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var permissions = model.Sections.Single(section => section.Heading == "Permissions");
        var toggleActions = permissions.ToggleRows.Select(row => row.ToggleAction).ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                TrayFlyoutAction.ToggleCanvasNode,
                TrayFlyoutAction.ToggleVoiceControls,
                TrayFlyoutAction.ToggleApprovalAlerts,
                TrayFlyoutAction.TogglePairingAlerts,
                TrayFlyoutAction.ToggleGatewayHealthAlerts,
                TrayFlyoutAction.ToggleDevicePermissionAlerts,
            },
            toggleActions);
    }

    [TestMethod]
    public void CanvasToggleReflectsSnapshotEnablement()
    {
        var on = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, canvasEnabled: true));
        var off = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, canvasEnabled: false));

        Assert.IsTrue(AllToggleRows(on).Single(row => row.ToggleAction == TrayFlyoutAction.ToggleCanvasNode).IsOn);
        Assert.IsFalse(AllToggleRows(off).Single(row => row.ToggleAction == TrayFlyoutAction.ToggleCanvasNode).IsOn);
    }

    [TestMethod]
    public void VoiceToggleReflectsPreferenceEnablement()
    {
        var on = TrayFlyoutComposer.Compose(
            Snapshot(
                RunningStatus(),
                GatewayRealtimeState.Connected,
                WindowsCanvasNodeState.Connected,
                preferences: AppPreferences.Default with { VoiceControlsEnabled = true }));
        var off = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        Assert.IsTrue(AllToggleRows(on).Single(row => row.ToggleAction == TrayFlyoutAction.ToggleVoiceControls).IsOn);
        Assert.IsFalse(AllToggleRows(off).Single(row => row.ToggleAction == TrayFlyoutAction.ToggleVoiceControls).IsOn);
    }

    [TestMethod]
    public void NotificationAlertTogglesReflectEachPreferenceFlag()
    {
        var preferences = AppPreferences.Default with
        {
            NotificationPreferences = new WindowsNotificationPreferences(
                ApprovalAlerts: true,
                PairingAlerts: false,
                GatewayHealthAlerts: true,
                DevicePermissionAlerts: false),
        };
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, preferences: preferences));

        var byAction = AllToggleRows(model).ToDictionary(row => row.ToggleAction);
        Assert.IsTrue(byAction[TrayFlyoutAction.ToggleApprovalAlerts].IsOn);
        Assert.IsFalse(byAction[TrayFlyoutAction.TogglePairingAlerts].IsOn);
        Assert.IsTrue(byAction[TrayFlyoutAction.ToggleGatewayHealthAlerts].IsOn);
        Assert.IsFalse(byAction[TrayFlyoutAction.ToggleDevicePermissionAlerts].IsOn);
    }

    [TestMethod]
    public void EveryToggleRowCarriesANonEmptyGlyph()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        foreach (var row in AllToggleRows(model))
        {
            Assert.IsFalse(string.IsNullOrEmpty(row.Glyph), $"Toggle {row.ToggleAction} is missing a glyph.");
        }
    }

    [TestMethod]
    public void PermissionTogglesDoNotLeakIntoActionRows()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.ToggleCanvasNode);
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.ToggleVoiceControls);
        CollectionAssert.DoesNotContain(actions, TrayFlyoutAction.ToggleApprovalAlerts);
    }

    [TestMethod]
    public void SupportSectionExposesEveryDiagnosticsEntryPointInOrder()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var support = model.Sections.Single(section => section.Heading == "Support");
        var supportActions = support.ActionRows.Select(row => row.Action).ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                TrayFlyoutAction.OpenNotificationHistory,
                TrayFlyoutAction.OpenActivityHistory,
                TrayFlyoutAction.OpenSupportSummary,
                TrayFlyoutAction.OpenCrashLog,
                TrayFlyoutAction.OpenAppLogFolder,
                TrayFlyoutAction.OpenGatewayLogFolder,
                TrayFlyoutAction.CreateSupportArtifact,
            },
            supportActions);
    }

    [TestMethod]
    public void SupportSectionRowsEachCarryANonEmptyGlyph()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var support = model.Sections.Single(section => section.Heading == "Support");
        foreach (var row in support.ActionRows)
        {
            Assert.IsFalse(string.IsNullOrEmpty(row.Glyph), $"Support action {row.Action} is missing a glyph.");
        }
    }

    [TestMethod]
    public void SupportSectionCarriesNoStatusOrToggleRows()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var support = model.Sections.Single(section => section.Heading == "Support");
        Assert.IsEmpty(support.StatusRows);
        Assert.IsEmpty(support.ToggleRows);
    }

    [TestMethod]
    public void SupportSectionAppearsBeforeTheExitAction()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected));

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        var lastSupportIndex = Array.FindLastIndex(actions, action => action == TrayFlyoutAction.CreateSupportArtifact);
        var exitIndex = Array.IndexOf(actions, TrayFlyoutAction.Exit);
        Assert.IsGreaterThan(lastSupportIndex, exitIndex);
        Assert.AreEqual(TrayFlyoutAction.Exit, actions[^1]);
    }

    [TestMethod]
    public void StatusQuickActionAndSupportSectionsAreUnaffectedAndPermissionsRemain()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, sessions: 2, approvals: 1));

        var headings = model.Sections.Select(section => section.Heading).ToArray();
        CollectionAssert.Contains(headings, "Permissions");
        CollectionAssert.Contains(headings, "Support");

        var actions = AllActionRows(model).Select(row => row.Action).ToArray();
        CollectionAssert.Contains(actions, TrayFlyoutAction.OpenShell);
        CollectionAssert.Contains(actions, TrayFlyoutAction.OpenLogs);
        CollectionAssert.Contains(actions, TrayFlyoutAction.RunGatewayRestart);
        Assert.IsTrue(AllStatusRows(model).Any(row => row.Label.StartsWith("Gateway:", StringComparison.Ordinal)));
        Assert.IsTrue(AllToggleRows(model).Any(row => row.ToggleAction == TrayFlyoutAction.ToggleCanvasNode));
    }

    [TestMethod]
    public void StatusAndQuickActionSectionsCarryNoToggleRows()
    {
        var model = TrayFlyoutComposer.Compose(
            Snapshot(RunningStatus(), GatewayRealtimeState.Connected, WindowsCanvasNodeState.Connected, sessions: 2, approvals: 1));

        var nonPermissionSections = model.Sections.Where(section => section.Heading != "Permissions");
        foreach (var section in nonPermissionSections)
        {
            Assert.IsEmpty(section.ToggleRows);
        }
    }
}
