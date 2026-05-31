using System.Globalization;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Windows.Native;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;
using XamlButton = Microsoft.UI.Xaml.Controls.Button;
using XamlCheckBox = Microsoft.UI.Xaml.Controls.CheckBox;
using XamlComboBox = Microsoft.UI.Xaml.Controls.ComboBox;
using XamlComboBoxItem = Microsoft.UI.Xaml.Controls.ComboBoxItem;
using XamlHorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using XamlOrientation = Microsoft.UI.Xaml.Controls.Orientation;
using XamlPasswordBox = Microsoft.UI.Xaml.Controls.PasswordBox;
using XamlTextBox = Microsoft.UI.Xaml.Controls.TextBox;
using XamlWebView2 = Microsoft.UI.Xaml.Controls.WebView2;

namespace OpenClaw.Windows;

/// <summary>
/// Programmatic WinUI shell for the Windows companion app.
/// </summary>
public sealed class MainWindow : Window
{
    // Shared brushes are mutated on theme changes so already-created pages repaint without being rebuilt.
    private static readonly SolidColorBrush AppBackgroundBrush = new();
    private static readonly SolidColorBrush CardBackgroundBrush = new();
    private static readonly SolidColorBrush CardStrokeBrush = new();
    private static readonly SolidColorBrush LayerFillBrush = new();
    private static readonly SolidColorBrush TextPrimaryBrush = new();
    private static readonly SolidColorBrush TextSecondaryBrush = new();
    private static readonly SolidColorBrush AccentBrush = new();
    private static readonly SolidColorBrush AccentTextBrush = new();
    private static readonly SolidColorBrush SuccessBrush = new();
    private static readonly SolidColorBrush CautionBrush = new();
    private static readonly SolidColorBrush CriticalBrush = new();
    private const double ScrollablePageScrollbarGutter = 24;

    private readonly WindowsCompanionState appState;
    private readonly WindowsCompanionCoordinator coordinator;
    private readonly WindowsCompanionCommandFactory commandFactory;
    private readonly ChatWorkspaceState chatState = new();
    private readonly Dictionary<string, UIElement> navigationPages = new();
    private readonly ContentControl navigationContent = new();
    private readonly TextBlock navigationGatewayStatusText = new();
    private readonly TextBlock statusText = new();
    private readonly TextBlock detailText = new();
    private readonly TextBlock commandErrorText = new();
    private readonly TextBlock homeGatewayStateText = new();
    private readonly TextBlock homeGatewayHealthText = new();
    private readonly TextBlock homeConnectionStateText = new();
    private readonly StackPanel homeGatewayRows = new() { Spacing = 8 };
    private readonly StackPanel homeConnectionRows = new() { Spacing = 8 };
    private readonly TextBlock homeActivityText = new();
    private readonly StackPanel homeNotificationRows = new() { Spacing = 8 };
    private readonly StackPanel onboardingList = new() { Spacing = 6 };
    private readonly TextBlock onboardingGuidedSummaryText = new();
    private readonly StackPanel onboardingGuidedActions = new() { Spacing = 8 };
    private readonly StackPanel homeOperatorRows = new() { Spacing = 8 };
    private readonly StackPanel chatMessages = new() { Spacing = 8 };
    private readonly StackPanel logsActivityRows = new() { Spacing = 8 };
    private readonly StackPanel logsNotificationRows = new() { Spacing = 8 };
    private readonly TextBlock supportSummaryText = new();
    private readonly StackPanel topologyRows = new() { Spacing = 8 };
    private readonly TextBlock chatStateText = new();
    private readonly TextBlock chatSessionText = new();
    private readonly TextBlock chatEmptyText = new();
    private readonly StackPanel chatEventMessages = new() { Spacing = 8 };
    private readonly StackPanel chatEventVisibilityControls = new() { Spacing = 6 };
    private readonly TextBlock chatEventVisibilitySummaryText = new();
    private readonly List<GatewayRealtimeEvent> chatRealtimeEvents = [];
    private string? chatEventVisibilityControlSignature;
    private bool updatingSessionEventVisibilityControls;
    private ScrollViewer? chatTranscriptScrollViewer;
    private bool chatScrollToBottomRequested;
    private readonly XamlButton chatRefreshButton = new();
    private readonly XamlButton chatComposerRefreshButton = new();
    private readonly XamlButton chatSendButton = new();
    private readonly TextBlock canvasStatusText = new();
    private readonly TextBlock canvasDetailText = new();
    private XamlWebView2? canvasWebView;
    private string? canvasTrustedA2UIUrl;
    private string? canvasNavigationTargetUrl;
    private string? canvasLoadedA2UIUrl;
    private ulong? canvasActiveNavigationId;
    private readonly SemaphoreSlim canvasBridgeScriptGate = new(1, 1);
    private bool canvasBridgeScriptInstalled;
    private readonly StackPanel sessionsList = new() { Spacing = 8 };
    private readonly TextBlock sessionsStatusText = new();
    private readonly XamlButton sessionsRefreshButton = new();
    private IReadOnlyList<SessionSummary> latestSessions = [];
    private string? latestSessionsError;
    private readonly StackPanel approvalsList = new() { Spacing = 8 };
    private readonly TextBlock approvalsStatusText = new();
    private readonly StackPanel pairingList = new() { Spacing = 8 };
    private readonly TextBlock pairingStatusText = new();
    private IReadOnlyList<PendingApproval> latestApprovals = [];
    private IReadOnlyList<PairingRequest> latestPairingRequests = [];
    private bool approvalsLoaded;
    private bool pairingLoaded;
    private int lastNotifiedApprovalCount;
    private int lastNotifiedPairingCount;
    private string? lastNotifiedGatewayHealth;
    private string? lastNotifiedDevicePermissionFailures;
    private readonly StackPanel deviceCapabilityCards = new() { Spacing = 12 };
    private readonly StackPanel mediaDevicesList = new() { Spacing = 6 };
    private readonly TextBlock nativeActionsText = new();
    private XamlTextBox screenRecordingDurationInput = new();
    private XamlTextBox screenRecordingFramesPerSecondInput = new();
    private TextBlock screenRecordingPlanText = new();
    private XamlButton cancelScreenRecordingButton = new();
    private XamlTextBox textToSpeechInput = new() { AcceptsReturn = true, MinHeight = 88, TextWrapping = TextWrapping.Wrap };
    private XamlComboBox textToSpeechVoiceInput = new();
    private readonly StackPanel logsDiagnosticsRows = new() { Spacing = 8 };
    private readonly StackPanel logsLocationCards = new() { Spacing = 12 };
    private readonly XamlTextBox rawLogsText = new();
    private readonly TextBlock logsText = new();
    private DateTimeOffset? lastGatewayStatusCheckedAt;
    private readonly TextBlock settingsText = new();
    private readonly StackPanel settingsStorageRows = new() { Spacing = 10 };
    private readonly TextBlock topologySummaryText = new();
    private readonly TextBlock tunnelStatusText = new();
    private readonly XamlCheckBox openMainWindowOnLaunchInput = new();
    private readonly XamlCheckBox approvalAlertsInput = new();
    private readonly XamlCheckBox pairingAlertsInput = new();
    private readonly XamlCheckBox gatewayHealthAlertsInput = new();
    private readonly XamlCheckBox devicePermissionAlertsInput = new();
    private readonly XamlCheckBox tunnelAutoStartInput = new();
    private readonly XamlCheckBox structuredDiagnosticsEnabledInput = new();
    private readonly XamlCheckBox blockUnsafeUrlsInput = new();
    private readonly XamlCheckBox redactSensitiveContentInput = new();
    private readonly XamlCheckBox canvasNodeEnabledInput = new();
    private readonly XamlCheckBox settingsVoiceControlsInput = new();
    private readonly XamlCheckBox settingsGlobalHotkeyInput = new();
    private readonly XamlComboBox themePreferenceInput = new();
    private readonly XamlComboBox accentColorInput = new();
    private readonly XamlComboBox colorThemeInput = new();
    private readonly ColorPicker customAccentColorInput = CreateAppearanceColorPicker();
    private readonly ColorPicker customColorThemeInput = CreateAppearanceColorPicker();
    private readonly StackPanel customAccentColorField = new() { Spacing = 4 };
    private readonly StackPanel customColorThemeField = new() { Spacing = 4 };
    private readonly XamlComboBox approvalPolicyInput = new();
    private readonly XamlTextBox chatInput = new() { AcceptsReturn = true, Height = 88, TextWrapping = TextWrapping.Wrap };
    private readonly XamlTextBox gatewayUrlInput = new();
    private readonly XamlPasswordBox gatewayTokenInput = new();
    private readonly XamlTextBox chatSessionInput = new();
    private readonly XamlTextBox tunnelHostInput = new();
    private readonly XamlTextBox tunnelRemoteHostInput = new();
    private readonly XamlTextBox tunnelLocalPortInput = new();
    private readonly XamlTextBox tunnelRemotePortInput = new();
    private readonly XamlTextBox diagnosticsPathInput = new();
    private readonly XamlTextBox activityRetentionCountInput = new();
    private readonly XamlTextBox notificationHistoryRetentionInput = new();
    private readonly StackPanel notificationRuleRows = new() { Spacing = 8 };
    private bool openMainWindowOnLaunch = AppPreferences.Default.OpenMainWindowOnLaunch;
    private WindowsThemePreference themePreference = AppPreferences.Default.ThemePreference;
    private WindowsAccentColorPreference accentColorPreference = AppPreferences.Default.AccentColorPreference;
    private Color? customAccentColor = AppPreferences.Default.CustomAccentColor;
    private WindowsColorThemePreference colorThemePreference = AppPreferences.Default.ColorThemePreference;
    private Color? customColorTheme = AppPreferences.Default.CustomColorTheme;
    private SessionEventVisibilityPreferences sessionEventVisibility = AppPreferences.Default.SessionEventVisibility;
    private WindowsNotificationPreferences notificationPreferences = WindowsNotificationPreferences.Default;
    private WindowsNotificationRulePreferences notificationRulePreferences = WindowsNotificationRulePreferences.Default;
    private WindowsTopologyPreferences topologyPreferences = AppPreferences.Default.Topology;
    private WindowsDiagnosticsPreferences diagnosticsPreferences = AppPreferences.Default.Diagnostics;
    private WindowsPolicyPreferences policyPreferences = AppPreferences.Default.Policy;
    private bool canvasNodeEnabled = AppPreferences.Default.CanvasNodeEnabled;
    private bool voiceControlsEnabled;
    private bool globalHotkeyEnabled;
    private IReadOnlyList<WindowsDevicePermissionStatus> latestDevicePermissionStatuses = [];
    private string mediaDeviceSummary = "Media devices have not been checked yet.";
    private string screenActionResult = "No screen capture run yet.";
    private string browserProxyActionResult = "Browser proxy guidance has not been checked yet.";
    private string cameraActionResult = "No camera capture run yet.";
    private string microphoneActionResult = "Voice controls have not been saved yet.";
    private string textToSpeechActionResult = "No speech clip generated yet.";
    private string notificationActionResult = "No notification sent yet.";
    private string hotkeyActionResult = "Global hotkey preference has not been saved yet.";
    private string overlayActionResult = "No overlay shown yet.";
    private WindowsTrayHost? trayHost;
    private TrayFlyoutWindow? trayFlyout;
    private AppPreferences currentPreferences = AppPreferences.Default;
    private WindowsGlobalHotkeyService? hotkeyService;
    private Window? overlayWindow;
    private NavigationView? navigationView;
    private bool exitRequested;
    private bool shutdownStarted;
    private bool updatingAppearanceInputs;
    private CancellationTokenSource? screenRecordingCancellation;
    private string? latestTextToSpeechPath;
    private string? latestSupportSummaryArtifactPath;
    private readonly WindowsNotificationRuleEvaluator notificationRuleEvaluator = new();
    private readonly WindowsGuidedOnboardingService guidedOnboarding = new();
    private readonly Dictionary<string, XamlTextBox> notificationRuleCategoryInputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, XamlComboBox> notificationRuleDestinationInputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, XamlCheckBox> notificationRuleEnabledInputs = new(StringComparer.OrdinalIgnoreCase);
    private IWindowsStringLocalizer Localizer => this.appState.Localizer;
    private string S(string resourceKey, string fallback) => this.Localizer.Get(resourceKey, fallback);
    private string SF(string resourceKey, string fallbackFormat, params object[] arguments) =>
        this.Localizer.Format(resourceKey, fallbackFormat, arguments);

    public MainWindow(WindowsCompanionState appState)
    {
        this.appState = appState;
        this.coordinator = new WindowsCompanionCoordinator(appState);
        this.commandFactory = new WindowsCompanionCommandFactory(
            () =>
            {
                this.ClearCommandError();
                return Task.CompletedTask;
            },
            this.ReportCommandError);
        this.Title = this.Localizer.Get("Shell.AppTitle", "OpenClaw");
        this.appState.Realtime.StateChanged += this.OnRealtimeStateChanged;
        this.appState.Realtime.EventReceived += this.OnRealtimeEventReceived;
        this.appState.CanvasNode.StateChanged += this.OnCanvasNodeStateChanged;
        this.appState.CanvasNode.CanvasSurfaceUrlChanged += this.OnCanvasSurfaceUrlChanged;
        this.appState.CanvasNode.InvokeAsync = this.HandleCanvasInvokeAsync;
        this.appState.Tunnel.StatusChanged += this.OnTunnelStatusChanged;
        this.Closed += this.OnClosed;
        this.AppWindow.Closing += this.OnAppWindowClosing;
        this.Content = this.BuildContent();
    }

    /// <summary>
    /// Applies the persisted theme preference to the root element and app-owned brushes.
    /// </summary>
    public void ApplyThemePreference(WindowsThemePreference preference)
    {
        this.themePreference = preference;
        if (this.Content is FrameworkElement root)
        {
            ApplyThemePreference(root, preference);
            this.ApplyCurrentPalette(root);
        }
    }

    public void ApplyAppearancePreferences(
        WindowsThemePreference themePreference,
        WindowsAccentColorPreference accentColorPreference,
        Color? customAccentColor,
        WindowsColorThemePreference colorThemePreference,
        Color? customColorTheme)
    {
        this.themePreference = themePreference;
        this.accentColorPreference = accentColorPreference;
        this.customAccentColor = NormalizeOpaqueColor(customAccentColor);
        this.colorThemePreference = colorThemePreference;
        this.customColorTheme = NormalizeOpaqueColor(customColorTheme);
        this.UpdateAppearanceColorPickers();
        if (this.Content is FrameworkElement root)
        {
            ApplyThemePreference(root, themePreference);
            this.ApplyCurrentPalette(root);
        }
    }

    /// <summary>
    /// Invoked when the operator chooses Exit in the tray flyout so the app can release process-wide
    /// resources (single-instance mutex, tray icon) and end the WinUI message loop.
    /// </summary>
    public Action? ExitRequested { get; set; }

    public void AttachTrayHost(WindowsTrayHost trayHost)
    {
        this.trayHost = trayHost;
        this.trayHost.TrayActivated += this.OnTrayActivated;
        this.UpdateTrayTooltip();
    }

    /// <summary>
    /// Refreshes the tray icon tooltip from the current snapshot so it stays live as gateway, node, and
    /// activity state change. The warning count is pending approvals + pairings + onboarding warnings.
    /// </summary>
    public void UpdateTrayTooltip()
    {
        if (this.trayHost is null)
        {
            return;
        }

        var snapshot = this.BuildTraySnapshot();
        var warningCount =
            snapshot.PendingApprovalCount +
            snapshot.PendingPairingCount +
            this.coordinator.OnboardingChecks.Count(check => check.State == OnboardingCheckState.Warning);
        this.trayHost.SetTooltip(TrayFlyoutComposer.BuildTooltip(snapshot, warningCount));
    }

    /// <summary>
    /// Builds a display-ready tray snapshot from current live gateway, realtime, node, and preference state.
    /// </summary>
    public WindowsTraySnapshot BuildTraySnapshot()
    {
        return WindowsTraySnapshot.Create(
            this.coordinator.GatewayStatus,
            this.coordinator.RealtimeState,
            this.appState.CanvasNode.State,
            this.canvasNodeEnabled,
            this.currentPreferences,
            this.latestSessions.Count,
            this.latestApprovals.Count,
            this.latestPairingRequests.Count,
            this.coordinator.LastActivity,
            this.appState.Notifications.Latest);
    }

    /// <summary>
    /// Shows the tray flyout anchored near the tray icon. Tray clicks arrive off the WinUI input flow,
    /// so the host marshals this onto the window dispatcher before calling in.
    /// </summary>
    private void OnTrayActivated(TrayAnchorPoint anchor)
    {
        this.DispatcherQueue.TryEnqueue(() => this.ShowTrayFlyout(anchor));
    }

    private void ShowTrayFlyout(TrayAnchorPoint anchor)
    {
        // A transient flyout gets a fresh window per open; reusing one hidden window accumulates
        // windowing state that faults after a few open/close cycles.
        this.trayFlyout?.RequestClose();
        var flyout = new TrayFlyoutWindow(this.RunTrayAction);
        this.trayFlyout = flyout;
        flyout.Closed += (_, _) =>
        {
            if (ReferenceEquals(this.trayFlyout, flyout))
            {
                this.trayFlyout = null;
            }
        };

        var snapshot = this.BuildTraySnapshot();
        var model = TrayFlyoutComposer.Compose(snapshot);
        var palette = this.ResolveCurrentPalette();
        flyout.ShowFor(model, palette, anchor);
    }

    /// <summary>
    /// Routes a tray flyout row action onto the existing app entry points.
    /// </summary>
    private void RunTrayAction(TrayFlyoutAction action)
    {
        switch (action)
        {
            case TrayFlyoutAction.OpenShell:
                this.ShowShell();
                break;
            case TrayFlyoutAction.OpenHome:
                this.ShowDestination(WindowsNavigationDestination.Home);
                break;
            case TrayFlyoutAction.OpenChat:
                this.ShowDestination(WindowsNavigationDestination.Chat);
                break;
            case TrayFlyoutAction.OpenCanvas:
                this.ShowDestination(WindowsNavigationDestination.Canvas);
                break;
            case TrayFlyoutAction.OpenSessions:
                this.ShowDestination(WindowsNavigationDestination.Sessions);
                break;
            case TrayFlyoutAction.OpenApprovals:
                this.ShowDestination(WindowsNavigationDestination.Approvals);
                break;
            case TrayFlyoutAction.OpenPairing:
                this.ShowDestination(WindowsNavigationDestination.Pairing);
                break;
            case TrayFlyoutAction.OpenSettings:
                this.ShowDestination(WindowsNavigationDestination.Settings);
                break;
            case TrayFlyoutAction.OpenLogs:
                this.ShowDestination(WindowsNavigationDestination.Logs);
                break;
            case TrayFlyoutAction.ConnectRealtime:
                this.ShowShell();
                this.ConnectGateway();
                break;
            case TrayFlyoutAction.DisconnectRealtime:
                this.ShowShell();
                this.DisconnectGateway();
                break;
            case TrayFlyoutAction.RunGatewayInstall:
                this.ShowShell();
                this.RunGatewayAction(GatewayCliAction.Install);
                break;
            case TrayFlyoutAction.RunGatewayStart:
                this.ShowShell();
                this.RunGatewayAction(GatewayCliAction.Start);
                break;
            case TrayFlyoutAction.RunGatewayStop:
                this.ShowShell();
                this.RunGatewayAction(GatewayCliAction.Stop);
                break;
            case TrayFlyoutAction.RunGatewayRestart:
                this.ShowShell();
                this.RunGatewayAction(GatewayCliAction.Restart);
                break;
            case TrayFlyoutAction.Exit:
                this.ExitRequested?.Invoke();
                break;
        }
    }

    /// <summary>
    /// Shows and activates the main window from tray callbacks or notification clicks.
    /// </summary>
    public void ShowShell()
    {
        this.AppWindow.Show();
        this.Activate();
    }

    public string GatewayStatusText => this.coordinator.GatewayStatus?.State ?? this.statusText.Text.Replace("Gateway: ", "", StringComparison.Ordinal);

    public string LatestActivityText => this.appState.Notifications.Latest is { } latest
        ? $"{latest.Title}: {latest.Message}"
        : this.coordinator.LastActivity ?? "None";

    /// <summary>
    /// Opens the shell directly to a navigation destination.
    /// </summary>
    public void ShowDestination(string destination)
    {
        this.ShowShell();
        this.SelectNavigationDestination(destination);
    }

    public void ShowLatestNotificationDestination()
    {
        this.ShowDestination(this.appState.Notifications.Latest?.Destination ?? WindowsNavigationDestination.Home);
    }

    /// <summary>
    /// Starts a realtime connect from tray/menu entry points and reports failures through the common command path.
    /// </summary>
    public async void ConnectGateway()
    {
        try
        {
            await this.ConnectRealtimeAsync();
        }
        catch (Exception ex)
        {
            this.ReportCommandError(ex);
        }
    }

    /// <summary>
    /// Tears down the realtime channel and Canvas node from tray/menu entry points.
    /// </summary>
    public async void DisconnectGateway()
    {
        try
        {
            await this.appState.CanvasNode.DisconnectAsync();
            await this.appState.Realtime.DisconnectAsync();
            await this.RecordActivityAsync("gateway", "Realtime disconnected", "Disconnected the Windows companion realtime channel.");
        }
        catch (Exception ex)
        {
            this.ReportCommandError(ex);
        }
    }

    /// <summary>
    /// Opens the gateway log when known, otherwise opens the app crash log path as the closest diagnostic.
    /// </summary>
    public void OpenLogs()
    {
        if (!string.IsNullOrWhiteSpace(this.coordinator.LogPath))
        {
            WindowsShell.OpenFileInExplorer(this.coordinator.LogPath);
            return;
        }
        WindowsShell.OpenFileInExplorer(CrashLog.Path);
    }

    /// <summary>
    /// Coordinates shutdown so background services stop before the WinUI window closes.
    /// </summary>
    public async Task ExitApplicationAsync()
    {
        this.exitRequested = true;
        await this.ShutdownAsync();
        this.Close();
    }

    /// <summary>
    /// Runs gateway lifecycle commands from tray callbacks.
    /// </summary>
    public async void RunGatewayAction(GatewayCliAction action)
    {
        try
        {
            await this.RunGatewayActionAsync(action);
        }
        catch (Exception ex)
        {
            this.ReportCommandError(ex);
        }
    }

    /// <summary>
    /// Builds the root navigation shell and starts the initial refresh after the visual tree exists.
    /// </summary>
    private UIElement BuildContent()
    {
        this.commandErrorText.Visibility = Visibility.Collapsed;
        this.commandErrorText.TextWrapping = TextWrapping.Wrap;
        this.commandErrorText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Firebrick);
        this.commandErrorText.Margin = new Thickness(24, 12, 24, 0);
        this.navigationGatewayStatusText.Text = "Gateway: Checking";
        this.navigationGatewayStatusText.Margin = new Thickness(12);
        this.navigationGatewayStatusText.TextWrapping = TextWrapping.Wrap;
        this.navigationGatewayStatusText.Opacity = 0.72;

        var navigation = new NavigationView
        {
            PaneTitle = this.Localizer.Get("Shell.Navigation.PaneTitle", "OpenClaw"),
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = true,
            OpenPaneLength = 220,
            CompactPaneLength = 48,
            PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
            PaneFooter = this.navigationGatewayStatusText,
            Content = this.navigationContent,
            Background = AppBackgroundBrush,
        };
        foreach (var item in this.appState.Navigation.PrimaryItems)
        {
            navigation.MenuItems.Add(CreateNavigationItem(item.Label, item.Destination, item.Glyph));
        }
        navigation.SelectionChanged += this.OnNavigationSelectionChanged;
        this.navigationView = navigation;

        if (navigation.MenuItems.FirstOrDefault() is NavigationViewItem homeItem)
        {
            navigation.SelectedItem = homeItem;
            this.ShowNavigationPage(homeItem);
        }

        var root = new Grid
        {
            Background = AppBackgroundBrush,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };
        root.Children.Add(this.commandErrorText);
        Grid.SetRow(navigation, 1);
        root.Children.Add(navigation);
        ApplyThemePreference(root, this.themePreference);
        this.ApplyCurrentPalette(root);
        root.ActualThemeChanged += (_, _) =>
        {
            this.ApplyCurrentPalette(root);
            this.PopulateAppearancePreviewItems(ResolveBrushTheme(root, this.themePreference));
        };

        _ = this.RefreshAllAsync();
        return root;
    }

    /// <summary>
    /// Wraps a page body with the common OpenClaw page header.
    /// </summary>
    private UIElement BuildPage(string title, FrameworkElement content)
    {
        var root = new Grid
        {
            Background = AppBackgroundBrush,
            Padding = new Thickness(24),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };

        root.Children.Add(this.BuildPageHeader(title));

        Grid.SetRow(content, 1);
        root.Children.Add(content);
        return root;
    }

    private UIElement BuildPageHeader(string title)
    {
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        header.Children.Add(new TextBlock
        {
            Text = $"Gateway protocol {this.appState.Summary.GatewayProtocolVersion}",
            Opacity = 0.72,
        });
        return header;
    }

    private static NavigationViewItem CreateNavigationItem(string label, string tag, string glyph)
    {
        return new NavigationViewItem
        {
            Content = label,
            Tag = tag,
            Icon = new FontIcon { Glyph = glyph },
        };
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            this.navigationContent.Content = this.GetNavigationPage(WindowsNavigationDestination.Settings);
            return;
        }

        if (args.SelectedItem is NavigationViewItem selectedItem)
        {
            this.ShowNavigationPage(selectedItem);
        }
    }

    private void ShowNavigationPage(NavigationViewItem selectedItem)
    {
        var tag = selectedItem.Tag as string;
        this.navigationContent.Content = this.GetNavigationPage(WindowsNavigationService.Normalize(tag));
    }

    private void SelectNavigationDestination(string destination)
    {
        destination = WindowsNavigationService.Normalize(destination);
        if (this.navigationView is null)
        {
            this.navigationContent.Content = this.GetNavigationPage(destination);
            return;
        }

        if (string.Equals(destination, WindowsNavigationDestination.Settings, StringComparison.Ordinal))
        {
            this.navigationView.SelectedItem = this.navigationView.SettingsItem;
            this.navigationContent.Content = this.GetNavigationPage(WindowsNavigationDestination.Settings);
            return;
        }

        foreach (var item in this.navigationView.MenuItems.OfType<NavigationViewItem>())
        {
            if (string.Equals(item.Tag as string, destination, StringComparison.Ordinal))
            {
                this.navigationView.SelectedItem = item;
                this.ShowNavigationPage(item);
                return;
            }
        }

        this.navigationContent.Content = this.GetNavigationPage(destination);
    }

    /// <summary>
    /// Lazily creates pages so controls keep local state while navigating.
    /// </summary>
    private UIElement GetNavigationPage(string tag)
    {
        if (this.navigationPages.TryGetValue(tag, out var page))
        {
            return page;
        }

        page = tag switch
        {
            WindowsNavigationDestination.Home => this.BuildPage(this.appState.Navigation.PageTitle(tag), Scrollable(this.BuildHomeDashboardPanel())),
            WindowsNavigationDestination.Chat => this.BuildChatPage(),
            WindowsNavigationDestination.Canvas => this.BuildPage(this.appState.Navigation.PageTitle(tag), this.BuildCanvasPanel()),
            WindowsNavigationDestination.Sessions => this.BuildPage(this.appState.Navigation.PageTitle(tag), Scrollable(this.BuildSessionsPanel())),
            WindowsNavigationDestination.Approvals => this.BuildPage(this.appState.Navigation.PageTitle(tag), Scrollable(this.BuildApprovalsPanel())),
            WindowsNavigationDestination.Pairing => this.BuildPage(this.appState.Navigation.PageTitle(tag), Scrollable(this.BuildPairingPanel())),
            WindowsNavigationDestination.Devices => this.BuildPage(this.appState.Navigation.PageTitle(tag), Scrollable(this.BuildDevicesPanel())),
            WindowsNavigationDestination.Logs => this.BuildPage(this.appState.Navigation.PageTitle(tag), Scrollable(this.BuildLogsPanel())),
            WindowsNavigationDestination.Settings => this.BuildPage(this.appState.Navigation.PageTitle(tag), Scrollable(this.BuildSettingsPanel())),
            _ => this.BuildPage(this.appState.Navigation.PageTitle(WindowsNavigationDestination.Home), Scrollable(this.BuildHomeDashboardPanel())),
        };
        this.navigationPages[tag] = page;
        return page;
    }

    private UIElement BuildHomeDashboardPanel()
    {
        var panel = new StackPanel { Spacing = 16 };
        this.homeGatewayStateText.Text = "Checking";
        this.homeGatewayHealthText.Text = "Checking";
        this.homeConnectionStateText.Text = this.coordinator.RealtimeState.ToString();
        this.homeActivityText.TextWrapping = TextWrapping.Wrap;
        this.detailText.TextWrapping = TextWrapping.Wrap;
        this.statusText.Visibility = Visibility.Collapsed;

        var summaryCards = new Grid
        {
            ColumnSpacing = 12,
            RowSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        AddSummaryCard(summaryCards, 0, "Gateway", this.homeGatewayStateText, "Service and RPC status");
        AddSummaryCard(summaryCards, 1, "Health", this.homeGatewayHealthText, "Onboarding readiness");
        AddSummaryCard(summaryCards, 2, "Connection", this.homeConnectionStateText, "Realtime channel");
        panel.Children.Add(summaryCards);

        panel.Children.Add(BuildDashboardCard("Gateway status", this.homeGatewayRows));
        panel.Children.Add(BuildDashboardCard("Connection state", this.homeConnectionRows));
        panel.Children.Add(BuildDashboardCard("Quick actions", this.BuildGatewayActions()));

        panel.Children.Add(BuildDashboardCard("Operator workflows", this.homeOperatorRows));
        panel.Children.Add(BuildDashboardCard("Onboarding health", this.onboardingList));
        this.onboardingGuidedSummaryText.TextWrapping = TextWrapping.Wrap;
        this.onboardingGuidedSummaryText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");
        panel.Children.Add(BuildDashboardCard(this.S("Shell.Home.GuidedActions.Title", "Guided next steps"), BuildSettingsSection(
            this.onboardingGuidedSummaryText,
            this.onboardingGuidedActions)));
        panel.Children.Add(BuildDashboardCard("Recent activity", this.homeActivityText));
        panel.Children.Add(BuildDashboardCard("Notification activity", this.homeNotificationRows));
        this.RenderHomeDashboard();
        return panel;
    }

    private UIElement BuildGatewayActions()
    {
        var buttons = new StackPanel { Spacing = 8 };
        buttons.Children.Add(ActionButton("Install", GatewayCliAction.Install));
        buttons.Children.Add(ActionButton("Start", GatewayCliAction.Start));
        buttons.Children.Add(ActionButton("Restart", GatewayCliAction.Restart));
        buttons.Children.Add(ActionButton("Stop", GatewayCliAction.Stop));
        buttons.Children.Add(new XamlButton
        {
            Content = "Connect",
            Command = this.CreateCommand(async () => await this.ConnectRealtimeAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Open Logs",
            Command = this.CreateCommand(() =>
            {
                if (!string.IsNullOrWhiteSpace(this.coordinator.LogPath))
                {
                    WindowsShell.OpenFileInExplorer(this.coordinator.LogPath);
                }
                return Task.CompletedTask;
            }),
        });
        return buttons;
    }

    private static void AddSummaryCard(Grid grid, int column, string title, TextBlock value, string caption)
    {
        value.FontSize = 22;
        value.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        value.TextWrapping = TextWrapping.Wrap;
        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });
        content.Children.Add(value);
        content.Children.Add(new TextBlock
        {
            Text = caption,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });

        var card = BuildDashboardCard(null, content);
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private static Border BuildDashboardCard(string? title, UIElement content)
    {
        var body = new StackPanel { Spacing = 10 };
        if (!string.IsNullOrWhiteSpace(title))
        {
            body.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
        }
        body.Children.Add(content);

        return new Border
        {
            Padding = new Thickness(16),
            Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = ResourceCornerRadius("OverlayCornerRadius"),
            Child = body,
        };
    }

    private static Brush ResourceBrush(string resourceName)
    {
        // Programmatic resource lookup returns fixed brush instances, so the shell uses owned brushes for themeable surfaces.
        if (string.Equals(resourceName, "CardBackgroundFillColorDefaultBrush", StringComparison.OrdinalIgnoreCase))
        {
            return CardBackgroundBrush;
        }
        if (string.Equals(resourceName, "CardStrokeColorDefaultBrush", StringComparison.OrdinalIgnoreCase))
        {
            return CardStrokeBrush;
        }
        if (string.Equals(resourceName, "LayerFillColorDefaultBrush", StringComparison.OrdinalIgnoreCase))
        {
            return LayerFillBrush;
        }
        if (string.Equals(resourceName, "TextFillColorPrimaryBrush", StringComparison.OrdinalIgnoreCase))
        {
            return TextPrimaryBrush;
        }
        if (string.Equals(resourceName, "TextFillColorSecondaryBrush", StringComparison.OrdinalIgnoreCase))
        {
            return TextSecondaryBrush;
        }
        if (string.Equals(resourceName, "AccentFillColorDefaultBrush", StringComparison.OrdinalIgnoreCase))
        {
            return AccentBrush;
        }
        if (string.Equals(resourceName, "TextOnAccentFillColorPrimaryBrush", StringComparison.OrdinalIgnoreCase))
        {
            return AccentTextBrush;
        }
        if (string.Equals(resourceName, "SystemFillColorSuccessBrush", StringComparison.OrdinalIgnoreCase))
        {
            return SuccessBrush;
        }
        if (string.Equals(resourceName, "SystemFillColorCautionBrush", StringComparison.OrdinalIgnoreCase))
        {
            return CautionBrush;
        }
        if (string.Equals(resourceName, "SystemFillColorCriticalBrush", StringComparison.OrdinalIgnoreCase))
        {
            return CriticalBrush;
        }

        if (Application.Current?.Resources.TryGetValue(resourceName, out var resource) == true &&
            resource is Brush brush)
        {
            return brush;
        }

        return TextPrimaryBrush;
    }

    private static CornerRadius ResourceCornerRadius(string resourceName)
    {
        if (Application.Current?.Resources.TryGetValue(resourceName, out var resource) == true &&
            resource is CornerRadius radius)
        {
            return radius;
        }

        return new CornerRadius(8);
    }

    private UIElement BuildChatPage()
    {
        var root = new Grid
        {
            Background = AppBackgroundBrush,
            Padding = new Thickness(24),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };
        root.Children.Add(this.BuildPageHeader("Chat"));

        var layout = new Grid
        {
            HorizontalAlignment = XamlHorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            RowSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
            },
        };
        layout.Children.Add(this.BuildChatHeader());
        var statusBar = this.BuildChatStatusBar();
        Grid.SetRow(statusBar, 1);
        layout.Children.Add(statusBar);
        var transcriptHost = this.BuildChatTranscriptHost();
        Grid.SetRow(transcriptHost, 2);
        layout.Children.Add(transcriptHost);
        var composer = this.BuildChatComposer();
        Grid.SetRow(composer, 3);
        layout.Children.Add(composer);
        Grid.SetRow(layout, 1);
        root.Children.Add(layout);
        this.RenderChatWorkspace();
        return root;
    }

    private FrameworkElement BuildChatHeader()
    {
        this.chatStateText.TextWrapping = TextWrapping.Wrap;
        this.chatStateText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        this.chatSessionText.TextWrapping = TextWrapping.Wrap;
        this.chatSessionText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");
        this.chatEmptyText.Text = this.Localizer.Get("Shell.Chat.EmptyState", "No messages in this session yet.");
        this.chatEmptyText.TextWrapping = TextWrapping.Wrap;
        this.chatEmptyText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");
        this.chatInput.PlaceholderText = this.Localizer.Get("Shell.Chat.Input.Placeholder", "Message the active OpenClaw session");
        AutomationProperties.SetName(this.chatInput, this.Localizer.Get("Shell.Chat.Input.AutomationName", "Message the active OpenClaw session"));
        this.chatInput.KeyDown -= this.OnChatInputKeyDown;
        this.chatInput.KeyDown += this.OnChatInputKeyDown;

        var header = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        var headerText = new StackPanel { Spacing = 4 };
        headerText.Children.Add(new TextBlock
        {
            Text = this.Localizer.Get("Shell.Chat.ConversationTitle", "Conversation"),
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        headerText.Children.Add(this.chatSessionText);
        header.Children.Add(headerText);

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        this.chatRefreshButton.Content = this.Localizer.Get("Shell.Chat.RefreshButtonLabel", "Refresh");
        this.chatRefreshButton.AccessKey = "R";
        this.chatRefreshButton.Command = this.CreateCommand(async () => await this.RefreshChatAsync());
        AutomationProperties.SetName(this.chatRefreshButton, this.Localizer.Get("Shell.Chat.RefreshButtonAutomationName", "Refresh session messages"));
        buttons.Children.Add(this.chatRefreshButton);
        Grid.SetColumn(buttons, 1);
        header.Children.Add(buttons);
        return header;
    }

    private FrameworkElement BuildChatStatusBar()
    {
        var hint = new TextBlock
        {
            Text = this.Localizer.Get("Shell.Chat.SendHint", "Ctrl+Enter to send"),
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(hint, 1);
        return new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            Background = ResourceBrush("LayerFillColorDefaultBrush"),
            BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = ResourceCornerRadius("OverlayCornerRadius"),
            Child = new Grid
            {
                ColumnSpacing = 12,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children =
                {
                    this.chatStateText,
                    hint,
                },
            },
        };
    }

    private FrameworkElement BuildChatTranscriptHost()
    {
        var transcriptBody = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                this.chatEmptyText,
                this.chatMessages,
            },
        };
        this.chatTranscriptScrollViewer = new ScrollViewer
        {
            Content = AddScrollbarGutter(transcriptBody),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        this.chatTranscriptScrollViewer.SizeChanged += (_, args) =>
            ResizeScrollableContent(this.chatTranscriptScrollViewer, args.NewSize.Width);

        return new Border
        {
            Padding = new Thickness(18),
            Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = ResourceCornerRadius("OverlayCornerRadius"),
            Child = this.chatTranscriptScrollViewer,
        };
    }

    private FrameworkElement BuildChatComposer()
    {
        this.chatComposerRefreshButton.Content = this.Localizer.Get("Shell.Chat.RefreshButtonLabel", "Refresh");
        this.chatComposerRefreshButton.Command = this.CreateCommand(async () => await this.RefreshChatAsync());
        AutomationProperties.SetName(
            this.chatComposerRefreshButton,
            this.Localizer.Get("Shell.Chat.RefreshButtonAutomationName", "Refresh session messages"));
        this.chatSendButton.Content = "Send";
        this.chatSendButton.AccessKey = "S";
        this.chatSendButton.Command = this.CreateCommand(async () => await this.SendChatAsync());
        if (this.chatSendButton.KeyboardAccelerators.Count == 0)
        {
            this.chatSendButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Enter,
                Modifiers = VirtualKeyModifiers.Control,
            });
        }
        AutomationProperties.SetName(this.chatSendButton, "Send message");
        var composerButtons = new StackPanel
        {
            Orientation = XamlOrientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = XamlHorizontalAlignment.Right,
            Children =
            {
                this.chatComposerRefreshButton,
                this.chatSendButton,
            },
        };

        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(new TextBlock
        {
            Text = "Compose",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        body.Children.Add(this.chatInput);
        body.Children.Add(composerButtons);
        return new Border
        {
            Padding = new Thickness(18),
            Background = ResourceBrush("LayerFillColorDefaultBrush"),
            BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = ResourceCornerRadius("OverlayCornerRadius"),
            Child = body,
        };
    }

    private FrameworkElement BuildCanvasPanel()
    {
        var root = new Grid
        {
            HorizontalAlignment = XamlHorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 480,
            RowSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };

        var header = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        var headerText = new StackPanel { Spacing = 4 };
        headerText.Children.Add(new TextBlock
        {
            Text = "A2UI Canvas",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        this.canvasStatusText.Text = "Canvas node disconnected";
        this.canvasStatusText.TextWrapping = TextWrapping.Wrap;
        this.canvasStatusText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");
        this.canvasDetailText.Text = "Connect to the gateway to load the advertised A2UI surface.";
        this.canvasDetailText.TextWrapping = TextWrapping.Wrap;
        this.canvasDetailText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");
        headerText.Children.Add(this.canvasStatusText);
        headerText.Children.Add(this.canvasDetailText);
        header.Children.Add(headerText);

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(new XamlButton
        {
            Content = "Connect Canvas",
            Command = this.CreateCommand(async () => await this.ConnectCanvasNodeAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Refresh A2UI",
            Command = this.CreateCommand(async () => await this.RefreshCanvasA2UIAsync(forceRefresh: true)),
        });
        Grid.SetColumn(buttons, 1);
        header.Children.Add(buttons);
        root.Children.Add(header);

        this.canvasWebView ??= this.CreateCanvasWebView();
        Grid.SetRow(this.canvasWebView, 1);
        root.Children.Add(this.canvasWebView);
        this.RenderCanvasState();
        return root;
    }

    private XamlWebView2 CreateCanvasWebView()
    {
        var webView = new XamlWebView2
        {
            HorizontalAlignment = XamlHorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 360,
        };
        webView.NavigationStarting += this.OnCanvasNavigationStarting;
        webView.NavigationCompleted += this.OnCanvasNavigationCompleted;
        webView.CoreWebView2Initialized += this.OnCanvasCoreWebView2Initialized;
        webView.Loaded += async (_, _) =>
        {
            try
            {
                await this.EnsureCanvasBridgeAsync(webView);
            }
            catch (Exception ex)
            {
                this.canvasDetailText.Text = $"WebView2 initialization failed: {ex.Message}";
                CrashLog.Write(ex);
            }
        };
        return webView;
    }

    private UIElement BuildSessionsPanel()
    {
        var panel = new StackPanel { Spacing = 16 };
        this.sessionsStatusText.TextWrapping = TextWrapping.Wrap;
        this.sessionsStatusText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");

        var header = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        var headerText = new StackPanel { Spacing = 4 };
        headerText.Children.Add(new TextBlock
        {
            Text = "Session browser",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        headerText.Children.Add(this.sessionsStatusText);
        header.Children.Add(headerText);

        this.sessionsRefreshButton.Content = "Refresh";
        this.sessionsRefreshButton.AccessKey = "R";
        this.sessionsRefreshButton.Command = this.CreateCommand(async () => await this.RefreshSessionsAsync());
        AutomationProperties.SetName(this.sessionsRefreshButton, "Refresh sessions");
        Grid.SetColumn(this.sessionsRefreshButton, 1);
        header.Children.Add(this.sessionsRefreshButton);
        panel.Children.Add(header);
        panel.Children.Add(this.sessionsList);
        this.RenderSessions();
        return panel;
    }

    private UIElement BuildApprovalsPanel()
    {
        var panel = new StackPanel { Spacing = 16 };
        this.approvalsStatusText.TextWrapping = TextWrapping.Wrap;
        this.approvalsStatusText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");

        var header = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        var headerText = new StackPanel { Spacing = 4 };
        headerText.Children.Add(new TextBlock
        {
            Text = "Command approvals",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        headerText.Children.Add(this.approvalsStatusText);
        header.Children.Add(headerText);

        var refreshButton = new XamlButton
        {
            Content = "Refresh",
            AccessKey = "R",
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            Command = this.CreateCommand(async () => await this.RefreshApprovalsAsync()),
        };
        AutomationProperties.SetName(refreshButton, "Refresh approvals");
        Grid.SetColumn(refreshButton, 1);
        header.Children.Add(refreshButton);
        panel.Children.Add(header);
        panel.Children.Add(this.approvalsList);
        this.RenderApprovals();
        return panel;
    }

    private UIElement BuildPairingPanel()
    {
        var panel = new StackPanel { Spacing = 16 };
        this.pairingStatusText.TextWrapping = TextWrapping.Wrap;
        this.pairingStatusText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");

        var header = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        var headerText = new StackPanel { Spacing = 4 };
        headerText.Children.Add(new TextBlock
        {
            Text = "Pairing requests",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        headerText.Children.Add(this.pairingStatusText);
        header.Children.Add(headerText);

        var refreshButton = new XamlButton
        {
            Content = "Refresh",
            AccessKey = "R",
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            Command = this.CreateCommand(async () => await this.RefreshPairingAsync()),
        };
        AutomationProperties.SetName(refreshButton, "Refresh pairing requests");
        Grid.SetColumn(refreshButton, 1);
        header.Children.Add(refreshButton);
        panel.Children.Add(header);
        panel.Children.Add(this.pairingList);
        this.RenderPairing();
        return panel;
    }

    private UIElement BuildDevicesPanel()
    {
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock
        {
            Text = "Windows capabilities",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        var refreshButton = new XamlButton
        {
            Content = "Refresh devices",
            AccessKey = "R",
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            Command = this.CreateCommand(async () => await this.RefreshDeviceCapabilitiesAsync()),
        };
        AutomationProperties.SetName(refreshButton, "Refresh Windows device capabilities");
        panel.Children.Add(refreshButton);
        panel.Children.Add(this.deviceCapabilityCards);
        panel.Children.Add(this.mediaDevicesList);
        panel.Children.Add(this.nativeActionsText);
        this.RenderDeviceCapabilityCards();
        return panel;
    }

    private UIElement BuildSettingsPanel()
    {
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock
        {
            Text = "Windows settings",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        this.gatewayUrlInput.PlaceholderText = AppPreferences.Default.GatewayUrl;
        AutomationProperties.SetName(this.gatewayUrlInput, "Gateway URL");
        AutomationProperties.SetName(this.gatewayTokenInput, "Gateway token");
        this.chatSessionInput.PlaceholderText = AppPreferences.Default.ChatSessionKey;
        AutomationProperties.SetName(this.chatSessionInput, "Chat session");
        this.tunnelHostInput.PlaceholderText = "user@example.com";
        AutomationProperties.SetName(this.tunnelHostInput, "SSH host");
        this.tunnelRemoteHostInput.PlaceholderText = AppPreferences.Default.Topology.RemoteHost;
        AutomationProperties.SetName(this.tunnelRemoteHostInput, "SSH remote host");
        this.tunnelLocalPortInput.PlaceholderText = AppPreferences.Default.Topology.LocalPort.ToString(CultureInfo.InvariantCulture);
        AutomationProperties.SetName(this.tunnelLocalPortInput, "SSH local port");
        this.tunnelRemotePortInput.PlaceholderText = AppPreferences.Default.Topology.RemotePort.ToString(CultureInfo.InvariantCulture);
        AutomationProperties.SetName(this.tunnelRemotePortInput, "SSH remote port");
        this.diagnosticsPathInput.PlaceholderText = this.appState.Diagnostics.DefaultPath;
        AutomationProperties.SetName(this.diagnosticsPathInput, "Structured diagnostics path");
        this.activityRetentionCountInput.PlaceholderText =
            AppPreferences.Default.Diagnostics.ActivityRetentionCount.ToString(CultureInfo.InvariantCulture);
        AutomationProperties.SetName(this.activityRetentionCountInput, "Activity retention count");
        this.notificationHistoryRetentionInput.PlaceholderText =
            WindowsNotificationRulePreferences.Default.HistoryRetentionCount.ToString(CultureInfo.InvariantCulture);
        AutomationProperties.SetName(this.notificationHistoryRetentionInput, this.S(
            "Shell.Settings.NotificationRules.Retention.AutomationName",
            "Notification history retention count"));
        this.themePreferenceInput.SelectionChanged -= this.OnThemePreferenceSelectionChanged;
        this.themePreferenceInput.Items.Clear();
        this.themePreferenceInput.Items.Add(CreateThemePreferenceItem("System", WindowsThemePreference.System));
        this.themePreferenceInput.Items.Add(CreateThemePreferenceItem("Light", WindowsThemePreference.Light));
        this.themePreferenceInput.Items.Add(CreateThemePreferenceItem("Dark", WindowsThemePreference.Dark));
        this.SelectThemePreference(this.themePreference);
        this.themePreferenceInput.SelectionChanged += this.OnThemePreferenceSelectionChanged;
        AutomationProperties.SetName(this.themePreferenceInput, "App theme");
        this.PopulateAppearancePreviewItems(this.ResolveCurrentBrushTheme());
        AutomationProperties.SetName(this.accentColorInput, "Accent color");
        AutomationProperties.SetName(this.colorThemeInput, "Color theme");
        AutomationProperties.SetName(this.customAccentColorInput, "Custom accent color");
        AutomationProperties.SetName(this.customColorThemeInput, "Custom color theme");
        this.customAccentColorInput.ColorChanged -= this.OnCustomAccentColorChanged;
        this.customColorThemeInput.ColorChanged -= this.OnCustomColorThemeChanged;
        this.UpdateAppearanceColorPickers();
        this.customAccentColorInput.ColorChanged += this.OnCustomAccentColorChanged;
        this.customColorThemeInput.ColorChanged += this.OnCustomColorThemeChanged;
        PopulateSettingsField(
            this.customAccentColorField,
            "Custom accent color",
            "Choose any highlight color when Accent color is set to Custom.",
            this.customAccentColorInput);
        PopulateSettingsField(
            this.customColorThemeField,
            "Custom color theme",
            "Choose any seed color when Color theme is set to Custom.",
            this.customColorThemeInput);
        this.UpdateCustomColorInputVisibility();
        this.approvalPolicyInput.Items.Clear();
        this.approvalPolicyInput.Items.Add(CreateApprovalPolicyItem(
            "Ask every time",
            WindowsApprovalPolicyPreference.AskEveryTime));
        this.approvalPolicyInput.Items.Add(CreateApprovalPolicyItem(
            "Allow safe commands automatically",
            WindowsApprovalPolicyPreference.AllowSafeCommands));
        this.approvalPolicyInput.Items.Add(CreateApprovalPolicyItem(
            "Deny risky commands automatically",
            WindowsApprovalPolicyPreference.DenyRiskyCommands));
        this.SelectApprovalPolicy(this.policyPreferences.ApprovalPolicy);
        AutomationProperties.SetName(this.approvalPolicyInput, "Approval policy");
        this.settingsText.TextWrapping = TextWrapping.Wrap;
        this.settingsText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");
        this.topologySummaryText.TextWrapping = TextWrapping.Wrap;
        this.tunnelStatusText.TextWrapping = TextWrapping.Wrap;
        this.supportSummaryText.TextWrapping = TextWrapping.Wrap;
        this.supportSummaryText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");
        this.PopulateNotificationRuleEditor(this.notificationRulePreferences);

        ConfigureSettingsToggle(
            this.openMainWindowOnLaunchInput,
            "Open main window on launch",
            value => this.openMainWindowOnLaunch = value);
        ConfigureSettingsToggle(
            this.approvalAlertsInput,
            "Approval alerts",
            value => this.notificationPreferences = this.notificationPreferences with { ApprovalAlerts = value });
        ConfigureSettingsToggle(
            this.pairingAlertsInput,
            "Pairing alerts",
            value => this.notificationPreferences = this.notificationPreferences with { PairingAlerts = value });
        ConfigureSettingsToggle(
            this.gatewayHealthAlertsInput,
            "Gateway health alerts",
            value => this.notificationPreferences = this.notificationPreferences with { GatewayHealthAlerts = value });
        ConfigureSettingsToggle(
            this.devicePermissionAlertsInput,
            "Device permission alerts",
            value => this.notificationPreferences = this.notificationPreferences with { DevicePermissionAlerts = value });
        ConfigureSettingsToggle(
            this.tunnelAutoStartInput,
            "Auto-start SSH tunnel",
            value => this.topologyPreferences = this.topologyPreferences with { AutoStartTunnel = value });
        ConfigureSettingsToggle(
            this.structuredDiagnosticsEnabledInput,
            "Write structured diagnostics",
            value => this.diagnosticsPreferences = this.diagnosticsPreferences with { StructuredDiagnosticsEnabled = value });
        ConfigureSettingsToggle(
            this.blockUnsafeUrlsInput,
            "Block unsafe URLs",
            value => this.policyPreferences = this.policyPreferences with { BlockUnsafeUrls = value });
        ConfigureSettingsToggle(
            this.redactSensitiveContentInput,
            "Redact sensitive content before saving diagnostics",
            value => this.policyPreferences = this.policyPreferences with { RedactSensitiveContent = value });
        ConfigureSettingsToggle(
            this.settingsVoiceControlsInput,
            "Enable voice controls",
            value => this.voiceControlsEnabled = value);
        ConfigureSettingsToggle(
            this.canvasNodeEnabledInput,
            "Enable Canvas and A2UI node",
            value => this.canvasNodeEnabled = value);
        ConfigureSettingsToggle(
            this.settingsGlobalHotkeyInput,
            "Register Ctrl+Shift+Space push-to-talk hotkey",
            value => this.globalHotkeyEnabled = value);

        panel.Children.Add(BuildDashboardCard("Gateway Connection", BuildSettingsSection(
            BuildSettingsField("Gateway URL", "Realtime Gateway WebSocket endpoint.", this.gatewayUrlInput),
            BuildSettingsField("Gateway token", "Stored in the Windows credential store when available.", this.gatewayTokenInput))));
        panel.Children.Add(BuildDashboardCard("Identity", BuildSettingsSection(
            BuildSettingsField("Chat session", "Default OpenClaw session key used by the native chat workspace.", this.chatSessionInput))));
        panel.Children.Add(BuildDashboardCard("Appearance", BuildSettingsSection(
            BuildSettingsField("Theme", "Choose System, Light, or Dark for the Windows companion.", this.themePreferenceInput),
            BuildSettingsField("Accent color", "Choose the Windows companion highlight color.", this.accentColorInput),
            this.customAccentColorField,
            BuildSettingsField("Color theme", "Choose the Windows companion surface palette.", this.colorThemeInput),
            this.customColorThemeField)));
        panel.Children.Add(BuildDashboardCard("Startup", BuildSettingsSection(
            this.openMainWindowOnLaunchInput,
            BuildReservedSettingsRow("Autostart", "Reserved", "Future tray startup preference."))));
        panel.Children.Add(BuildDashboardCard("Notifications", BuildSettingsSection(
            this.approvalAlertsInput,
            this.pairingAlertsInput,
            this.gatewayHealthAlertsInput,
            this.devicePermissionAlertsInput,
            BuildSettingsField(
                this.S("Shell.Settings.NotificationRules.Retention.Label", "Notification history retention"),
                this.S(
                    "Shell.Settings.NotificationRules.Retention.Detail",
                    "Maximum number of persisted notification entries kept locally."),
                this.notificationHistoryRetentionInput),
            this.BuildNotificationRuleEditor())));
        panel.Children.Add(BuildDashboardCard("Topology and Tunnels", BuildSettingsSection(
            BuildSettingsField("SSH host", "Destination passed to the local ssh client.", this.tunnelHostInput),
            BuildSettingsField("Remote host", "Host forwarded by the tunnel after ssh connects.", this.tunnelRemoteHostInput),
            BuildSettingsField("Local port", "Local listener used for forwarded traffic.", this.tunnelLocalPortInput),
            BuildSettingsField("Remote port", "Remote port forwarded through the SSH tunnel.", this.tunnelRemotePortInput),
            this.tunnelAutoStartInput,
            this.topologySummaryText,
            this.topologyRows,
            BuildSettingsSection(
                this.BuildSettingsActionButton("Start tunnel", () => this.RunTunnelFromSettingsAsync()),
                this.BuildSettingsActionButton("Stop tunnel", () =>
                {
                    this.appState.Tunnel.Stop();
                    return this.RefreshTopologyAsync();
                }),
                this.BuildSettingsActionButton("Refresh topology", () => this.RefreshTopologyAsync())))));
        panel.Children.Add(BuildDashboardCard("Diagnostics and History", BuildSettingsSection(
            this.structuredDiagnosticsEnabledInput,
            BuildSettingsField("Diagnostics path", "JSONL diagnostics file written by the Windows companion.", this.diagnosticsPathInput),
            BuildSettingsField("History retention", "Maximum number of persisted activity rows kept locally.", this.activityRetentionCountInput),
            this.tunnelStatusText)));
        panel.Children.Add(BuildDashboardCard("Approval Policy", BuildSettingsSection(
            BuildSettingsField("Default policy", "Local auto-resolution rules for gateway execution approvals.", this.approvalPolicyInput),
            this.blockUnsafeUrlsInput,
            this.redactSensitiveContentInput)));
        panel.Children.Add(BuildDashboardCard("Devices", BuildSettingsSection(
            this.canvasNodeEnabledInput,
            this.settingsVoiceControlsInput,
            this.settingsGlobalHotkeyInput)));
        panel.Children.Add(BuildDashboardCard(this.S("Shell.Settings.RuntimeFeatures.Title", "Runtime feature storage"), BuildSettingsSection(
            BuildDashboardRow(this.S("Shell.Settings.RuntimeFeatures.Captures", "Captures"), this.appState.DeviceCapabilities.CaptureRoot),
            BuildDashboardRow(this.S("Shell.Settings.RuntimeFeatures.Speech", "Speech clips"), this.appState.TextToSpeech.OutputRoot),
            BuildDashboardRow(this.S("Shell.Settings.RuntimeFeatures.BrowserProxy", "Browser proxy"), this.browserProxyActionResult))));
        panel.Children.Add(BuildDashboardCard("Storage and Logs", this.settingsStorageRows));
        panel.Children.Add(BuildDashboardCard("About", BuildSettingsSection(
            BuildDashboardRow("Product", "OpenClaw Windows companion"),
            BuildDashboardRow("Gateway protocol", this.appState.Summary.GatewayProtocolVersion.ToString(CultureInfo.InvariantCulture)),
            this.settingsText)));
        this.RenderSettingsStorage();

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        var saveButton = new XamlButton
        {
            Content = "Save",
            AccessKey = "S",
            Command = this.CreateCommand(async () => await this.SaveSettingsAsync()),
        };
        AutomationProperties.SetName(saveButton, "Save Windows settings");
        buttons.Children.Add(saveButton);
        var refreshButton = new XamlButton
        {
            Content = "Refresh",
            AccessKey = "R",
            Command = this.CreateCommand(async () => await this.RefreshAllAsync()),
        };
        AutomationProperties.SetName(refreshButton, "Refresh Windows settings");
        buttons.Children.Add(refreshButton);
        panel.Children.Add(buttons);
        return panel;
    }

    private static StackPanel BuildSettingsSection(params UIElement[] rows)
    {
        var section = new StackPanel { Spacing = 10 };
        foreach (var row in rows)
        {
            section.Children.Add(row);
        }
        return section;
    }

    private static XamlComboBoxItem CreateThemePreferenceItem(string label, WindowsThemePreference preference)
    {
        return new XamlComboBoxItem
        {
            Content = label,
            Tag = preference,
        };
    }

    private static XamlComboBoxItem CreateApprovalPolicyItem(string label, WindowsApprovalPolicyPreference policy)
    {
        return new XamlComboBoxItem
        {
            Content = label,
            Tag = policy,
        };
    }

    private static ColorPicker CreateAppearanceColorPicker()
    {
        return new ColorPicker
        {
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            MaxWidth = 360,
        };
    }

    private XamlComboBoxItem CreateAccentColorItem(
        string label,
        WindowsAccentColorPreference preference,
        ElementTheme theme)
    {
        var fill = preference == WindowsAccentColorPreference.Custom
            ? this.ResolveCustomAccentColor()
            : WindowsThemePaletteResolver.ResolveAccentColor(preference, theme);
        var swatch = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(fill),
            BorderBrush = preference == WindowsAccentColorPreference.System
                ? ResourceBrush("TextFillColorSecondaryBrush")
                : null,
            BorderThickness = preference == WindowsAccentColorPreference.System ? new Thickness(2) : new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (preference == WindowsAccentColorPreference.System)
        {
            swatch.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        return new XamlComboBoxItem
        {
            Tag = preference,
            Content = new StackPanel
            {
                Orientation = XamlOrientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    swatch,
                    new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center },
                },
            },
        };
    }

    private XamlComboBoxItem CreateColorThemeItem(
        string label,
        WindowsColorThemePreference preference,
        ElementTheme theme)
    {
        var palette = WindowsThemePaletteResolver.Resolve(
            theme,
            this.accentColorPreference,
            preference,
            TryGetApplicationSystemAccentColor(),
            this.customAccentColor,
            this.ResolveCustomColorTheme());
        var preview = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 4 };
        preview.Children.Add(CreatePreviewSwatch(palette.AppBackgroundColor, palette.CardStrokeColor));
        preview.Children.Add(CreatePreviewSwatch(palette.CardBackgroundColor, palette.CardStrokeColor));
        preview.Children.Add(CreatePreviewSwatch(palette.TextPrimaryColor, palette.TextPrimaryColor));
        preview.Children.Add(CreatePreviewSwatch(Microsoft.UI.Colors.Transparent, palette.CardStrokeColor, new Thickness(2)));

        return new XamlComboBoxItem
        {
            Tag = preference,
            Content = new StackPanel
            {
                Orientation = XamlOrientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    preview,
                    new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center },
                },
            },
        };
    }

    private static Border CreatePreviewSwatch(Color fill, Color border, Thickness? borderThickness = null)
    {
        return new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(fill),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = borderThickness ?? new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static UIElement BuildSettingsField(string label, string detail, Control input)
    {
        var field = new StackPanel { Spacing = 4 };
        PopulateSettingsField(field, label, detail, input);
        return field;
    }

    private static void PopulateSettingsField(StackPanel field, string label, string detail, Control input)
    {
        field.Children.Clear();
        field.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        field.Children.Add(new TextBlock
        {
            Text = detail,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });
        field.Children.Add(input);
    }

    private static UIElement BuildReservedSettingsRow(string label, string state, string detail)
    {
        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(BuildDashboardRow(label, state));
        row.Children.Add(new TextBlock
        {
            Text = detail,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });
        return row;
    }

    private XamlButton BuildSettingsActionButton(string label, Func<Task> execute)
    {
        return new XamlButton
        {
            Content = label,
            Command = this.CreateCommand(execute),
        };
    }

    private UIElement BuildNotificationRuleEditor()
    {
        var actions = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        actions.Children.Add(this.BuildSettingsActionButton(
            this.S("Shell.Settings.NotificationRules.ResetButton", "Reset rules"),
            () =>
            {
                this.notificationRulePreferences = WindowsNotificationRulePreferences.Default;
                this.PopulateNotificationRuleEditor(this.notificationRulePreferences);
                return Task.CompletedTask;
            }));
        return BuildSettingsSection(
            new TextBlock
            {
                Text = this.S(
                    "Shell.Settings.NotificationRules.Description",
                    "Edit the stored category and deep-link destination used when companion notifications are saved."),
                TextWrapping = TextWrapping.Wrap,
                Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
            },
            actions,
            this.notificationRuleRows);
    }

    private void PopulateNotificationRuleEditor(WindowsNotificationRulePreferences preferences)
    {
        this.notificationRulePreferences = WindowsNotificationRuleEvaluator.NormalizePreferences(preferences);
        this.notificationHistoryRetentionInput.Text = this.notificationRulePreferences.HistoryRetentionCount.ToString(CultureInfo.InvariantCulture);
        this.notificationRuleRows.Children.Clear();
        this.notificationRuleCategoryInputs.Clear();
        this.notificationRuleDestinationInputs.Clear();
        this.notificationRuleEnabledInputs.Clear();
        foreach (var rule in this.notificationRulePreferences.Rules.OrderBy(static rule => rule.Kind))
        {
            var categoryInput = new XamlTextBox { Text = rule.Category };
            var destinationInput = this.CreateNotificationDestinationInput(rule.Destination);
            var enabledInput = new XamlCheckBox
            {
                Content = this.S("Shell.Settings.NotificationRules.EnabledLabel", "Enabled"),
                IsChecked = rule.Enabled,
            };
            ApplyAccentCheckedState(enabledInput);
            AutomationProperties.SetName(enabledInput, this.SF(
                "Shell.Settings.NotificationRules.Enabled.AutomationName",
                "Enable {0} notification rule",
                rule.Kind));

            this.notificationRuleCategoryInputs[rule.Id] = categoryInput;
            this.notificationRuleDestinationInputs[rule.Id] = destinationInput;
            this.notificationRuleEnabledInputs[rule.Id] = enabledInput;
            this.notificationRuleRows.Children.Add(BuildDashboardCard(
                null,
                BuildSettingsSection(
                    BuildDashboardRow(
                        this.S("Shell.Settings.NotificationRules.KindLabel", "Notification"),
                        this.NotificationKindLabel(rule.Kind)),
                    enabledInput,
                    BuildSettingsField(
                        this.S("Shell.Settings.NotificationRules.CategoryLabel", "Category"),
                        this.S(
                            "Shell.Settings.NotificationRules.CategoryDetail",
                            "Stored label used when the notification is copied into history."),
                        categoryInput),
                    BuildSettingsField(
                        this.S("Shell.Settings.NotificationRules.DestinationLabel", "Destination"),
                        this.S(
                            "Shell.Settings.NotificationRules.DestinationDetail",
                            "Page opened when the notification is selected from history or the tray."),
                        destinationInput))));
        }
    }

    private XamlComboBox CreateNotificationDestinationInput(string destination)
    {
        var comboBox = new XamlComboBox();
        foreach (var knownDestination in GetNotificationDestinations())
        {
            comboBox.Items.Add(new XamlComboBoxItem
            {
                Content = this.appState.Navigation.PageTitle(knownDestination),
                Tag = knownDestination,
            });
        }

        this.SelectNotificationDestination(comboBox, destination);
        AutomationProperties.SetName(comboBox, this.S(
            "Shell.Settings.NotificationRules.Destination.AutomationName",
            "Notification destination"));
        return comboBox;
    }

    private void SelectNotificationDestination(XamlComboBox comboBox, string destination)
    {
        var normalized = WindowsNavigationService.Normalize(destination);
        foreach (var item in comboBox.Items.OfType<XamlComboBoxItem>())
        {
            if (item.Tag is string itemDestination &&
                string.Equals(itemDestination, normalized, StringComparison.Ordinal))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private static IReadOnlyList<string> GetNotificationDestinations()
    {
        return
        [
            WindowsNavigationDestination.Home,
            WindowsNavigationDestination.Approvals,
            WindowsNavigationDestination.Pairing,
            WindowsNavigationDestination.Devices,
            WindowsNavigationDestination.Logs,
            WindowsNavigationDestination.Settings,
        ];
    }

    private string NotificationKindLabel(WindowsNotificationKind kind)
    {
        return kind switch
        {
            WindowsNotificationKind.Approval => this.S("Shell.Settings.NotificationRules.Kind.Approval", "Approval"),
            WindowsNotificationKind.Pairing => this.S("Shell.Settings.NotificationRules.Kind.Pairing", "Pairing"),
            WindowsNotificationKind.GatewayHealth => this.S("Shell.Settings.NotificationRules.Kind.GatewayHealth", "Gateway health"),
            WindowsNotificationKind.DevicePermission => this.S("Shell.Settings.NotificationRules.Kind.DevicePermission", "Device permission"),
            _ => this.S("Shell.Settings.NotificationRules.Kind.General", "General"),
        };
    }

    private WindowsNotificationRulePreferences CollectNotificationRulePreferences()
    {
        var rules = this.notificationRulePreferences.Rules
            .Select(rule =>
            {
                var destination = this.notificationRuleDestinationInputs.TryGetValue(rule.Id, out var destinationInput) &&
                    destinationInput.SelectedItem is XamlComboBoxItem { Tag: string selectedDestination }
                    ? selectedDestination
                    : rule.Destination;
                var category = this.notificationRuleCategoryInputs.TryGetValue(rule.Id, out var categoryInput)
                    ? categoryInput.Text
                    : rule.Category;
                var enabled = this.notificationRuleEnabledInputs.TryGetValue(rule.Id, out var enabledInput) &&
                    enabledInput.IsChecked == true;
                return rule with
                {
                    Category = category,
                    Destination = destination,
                    Enabled = enabled,
                };
            })
            .ToArray();
        return WindowsNotificationRuleEvaluator.NormalizePreferences(new WindowsNotificationRulePreferences(
            ParsePositiveIntOrDefault(
                this.notificationHistoryRetentionInput.Text,
                this.notificationRulePreferences.HistoryRetentionCount,
                WindowsNotificationRulePreferences.Default.HistoryRetentionCount),
            rules));
    }

    private static void ConfigureSettingsToggle(XamlCheckBox toggle, string label, Action<bool> update)
    {
        toggle.Content = label;
        ApplyAccentCheckedState(toggle);
        toggle.Checked += (_, _) => update(true);
        toggle.Unchecked += (_, _) => update(false);
        AutomationProperties.SetName(toggle, label);
    }

    private UIElement BuildChatEventVisibilityPanel()
    {
        var panel = new StackPanel { Spacing = 10 };
        this.chatEventVisibilitySummaryText.TextWrapping = TextWrapping.Wrap;
        this.chatEventVisibilitySummaryText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");
        panel.Children.Add(this.chatEventVisibilitySummaryText);

        var actions = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        actions.Children.Add(new XamlButton
        {
            Content = "Show all",
            Command = this.CreateCommand(async () =>
                await this.UpdateSessionEventVisibilityAsync(SessionEventVisibility.ShowAll)),
        });
        actions.Children.Add(new XamlButton
        {
            Content = "Hide operational",
            Command = this.CreateCommand(async () =>
                await this.UpdateSessionEventVisibilityAsync(SessionEventVisibility.HideOperational)),
        });
        actions.Children.Add(new XamlButton
        {
            Content = "Chat only",
            Command = this.CreateCommand(async () =>
                await this.UpdateSessionEventVisibilityAsync(SessionEventVisibility.ChatOnly)),
        });
        actions.Children.Add(new XamlButton
        {
            Content = "Reset",
            Command = this.CreateCommand(async () =>
                await this.UpdateSessionEventVisibilityAsync(_ => AppPreferences.Default.SessionEventVisibility)),
        });
        panel.Children.Add(actions);
        panel.Children.Add(this.chatEventVisibilityControls);
        return panel;
    }

    private async Task UpdateSessionEventVisibilityAsync(
        Func<SessionEventVisibilityPreferences, SessionEventVisibilityPreferences> update)
    {
        var preferences = await this.appState.Preferences.UpdateAsync(current =>
        {
            var nextVisibility = update(current.SessionEventVisibility.WithObservedEvents(this.chatRealtimeEvents));
            return current with { SessionEventVisibility = nextVisibility };
        });
        this.sessionEventVisibility = preferences.SessionEventVisibility.WithObservedEvents(this.chatRealtimeEvents);
        this.RenderGatewayEvents();
    }

    private void OnThemePreferenceSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (this.themePreferenceInput.SelectedItem is XamlComboBoxItem { Tag: WindowsThemePreference preference })
        {
            this.ApplyThemePreference(preference);
            this.PopulateAppearancePreviewItems(this.ResolveCurrentBrushTheme());
        }
    }

    private void OnAccentColorSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (this.accentColorInput.SelectedItem is XamlComboBoxItem { Tag: WindowsAccentColorPreference preference })
        {
            if (preference == WindowsAccentColorPreference.Custom)
            {
                this.customAccentColor ??= DefaultCustomAccentColor();
                this.UpdateAppearanceColorPickers();
            }

            this.ApplyAccentColorPreference(preference);
        }
    }

    private void OnColorThemeSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (this.colorThemeInput.SelectedItem is XamlComboBoxItem { Tag: WindowsColorThemePreference preference })
        {
            if (preference == WindowsColorThemePreference.Custom)
            {
                this.customColorTheme ??= DefaultCustomColorTheme();
                this.UpdateAppearanceColorPickers();
            }

            this.ApplyColorThemePreference(preference);
        }
    }

    private void OnCustomAccentColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (this.updatingAppearanceInputs)
        {
            return;
        }

        this.customAccentColor = NormalizeOpaqueColor(args.NewColor);
        this.ApplyAccentColorPreference(WindowsAccentColorPreference.Custom);
        this.PopulateAppearancePreviewItems(this.ResolveCurrentBrushTheme());
    }

    private void OnCustomColorThemeChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (this.updatingAppearanceInputs)
        {
            return;
        }

        this.customColorTheme = NormalizeOpaqueColor(args.NewColor);
        this.ApplyColorThemePreference(WindowsColorThemePreference.Custom);
        this.PopulateAppearancePreviewItems(this.ResolveCurrentBrushTheme());
    }

    private void OnScreenRecordingSettingsChanged(object sender, TextChangedEventArgs args)
    {
        this.UpdateScreenRecordingPlanPreview();
    }

    private void SelectThemePreference(WindowsThemePreference preference)
    {
        foreach (var item in this.themePreferenceInput.Items.OfType<XamlComboBoxItem>())
        {
            if (item.Tag is WindowsThemePreference itemPreference && itemPreference == preference)
            {
                this.themePreferenceInput.SelectedItem = item;
                return;
            }
        }
    }

    private void SelectAccentColor(WindowsAccentColorPreference preference)
    {
        foreach (var item in this.accentColorInput.Items.OfType<XamlComboBoxItem>())
        {
            if (item.Tag is WindowsAccentColorPreference itemPreference && itemPreference == preference)
            {
                this.accentColorInput.SelectedItem = item;
                return;
            }
        }
    }

    private void SelectColorTheme(WindowsColorThemePreference preference)
    {
        foreach (var item in this.colorThemeInput.Items.OfType<XamlComboBoxItem>())
        {
            if (item.Tag is WindowsColorThemePreference itemPreference && itemPreference == preference)
            {
                this.colorThemeInput.SelectedItem = item;
                return;
            }
        }
    }

    private void SelectApprovalPolicy(WindowsApprovalPolicyPreference preference)
    {
        foreach (var item in this.approvalPolicyInput.Items.OfType<XamlComboBoxItem>())
        {
            if (item.Tag is WindowsApprovalPolicyPreference itemPreference && itemPreference == preference)
            {
                this.approvalPolicyInput.SelectedItem = item;
                return;
            }
        }
    }

    private void PopulateAppearancePreviewItems(ElementTheme theme)
    {
        this.PopulateAccentColorItems(theme);
        this.PopulateColorThemeItems(theme);
    }

    private void PopulateAccentColorItems(ElementTheme theme)
    {
        this.accentColorInput.SelectionChanged -= this.OnAccentColorSelectionChanged;
        this.accentColorInput.Items.Clear();
        this.accentColorInput.Items.Add(this.CreateAccentColorItem("System", WindowsAccentColorPreference.System, theme));
        this.accentColorInput.Items.Add(this.CreateAccentColorItem("Blue", WindowsAccentColorPreference.Blue, theme));
        this.accentColorInput.Items.Add(this.CreateAccentColorItem("Teal", WindowsAccentColorPreference.Teal, theme));
        this.accentColorInput.Items.Add(this.CreateAccentColorItem("Green", WindowsAccentColorPreference.Green, theme));
        this.accentColorInput.Items.Add(this.CreateAccentColorItem("Orange", WindowsAccentColorPreference.Orange, theme));
        this.accentColorInput.Items.Add(this.CreateAccentColorItem("Rose", WindowsAccentColorPreference.Rose, theme));
        this.accentColorInput.Items.Add(this.CreateAccentColorItem("Purple", WindowsAccentColorPreference.Purple, theme));
        this.accentColorInput.Items.Add(this.CreateAccentColorItem("Custom", WindowsAccentColorPreference.Custom, theme));
        this.SelectAccentColor(this.accentColorPreference);
        this.accentColorInput.SelectionChanged += this.OnAccentColorSelectionChanged;
    }

    private void PopulateColorThemeItems(ElementTheme theme)
    {
        this.colorThemeInput.SelectionChanged -= this.OnColorThemeSelectionChanged;
        this.colorThemeInput.Items.Clear();
        this.colorThemeInput.Items.Add(this.CreateColorThemeItem("Default", WindowsColorThemePreference.Default, theme));
        this.colorThemeInput.Items.Add(this.CreateColorThemeItem("Slate", WindowsColorThemePreference.Slate, theme));
        this.colorThemeInput.Items.Add(this.CreateColorThemeItem("Forest", WindowsColorThemePreference.Forest, theme));
        this.colorThemeInput.Items.Add(this.CreateColorThemeItem("Ocean", WindowsColorThemePreference.Ocean, theme));
        this.colorThemeInput.Items.Add(this.CreateColorThemeItem("Ember", WindowsColorThemePreference.Ember, theme));
        this.colorThemeInput.Items.Add(this.CreateColorThemeItem("High Contrast", WindowsColorThemePreference.HighContrast, theme));
        this.colorThemeInput.Items.Add(this.CreateColorThemeItem("Custom", WindowsColorThemePreference.Custom, theme));
        this.SelectColorTheme(this.colorThemePreference);
        this.colorThemeInput.SelectionChanged += this.OnColorThemeSelectionChanged;
    }

    private ElementTheme ResolveCurrentBrushTheme()
    {
        return this.Content is FrameworkElement root
            ? ResolveBrushTheme(root, this.themePreference)
            : ElementTheme.Light;
    }

    private void ApplyAccentColorPreference(WindowsAccentColorPreference preference)
    {
        this.accentColorPreference = preference;
        this.UpdateCustomColorInputVisibility();
        if (this.Content is FrameworkElement root)
        {
            this.ApplyCurrentPalette(root);
        }
    }

    private void ApplyColorThemePreference(WindowsColorThemePreference preference)
    {
        this.colorThemePreference = preference;
        this.UpdateCustomColorInputVisibility();
        if (this.Content is FrameworkElement root)
        {
            this.ApplyCurrentPalette(root);
        }
    }

    private static void ApplyThemePreference(FrameworkElement root, WindowsThemePreference preference)
    {
        if (preference == WindowsThemePreference.System)
        {
            root.ClearValue(FrameworkElement.RequestedThemeProperty);
            return;
        }

        root.RequestedTheme = preference switch
        {
            WindowsThemePreference.Light => ElementTheme.Light,
            WindowsThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private static ElementTheme ResolveBrushTheme(FrameworkElement root, WindowsThemePreference preference)
    {
        return preference switch
        {
            WindowsThemePreference.Light => ElementTheme.Light,
            WindowsThemePreference.Dark => ElementTheme.Dark,
            _ => root.ActualTheme,
        };
    }

    private void ApplyCurrentPalette(FrameworkElement root)
    {
        var palette = WindowsThemePaletteResolver.Resolve(
            ResolveBrushTheme(root, this.themePreference),
            this.accentColorPreference,
            this.colorThemePreference,
            TryGetApplicationSystemAccentColor(),
            this.customAccentColor,
            this.customColorTheme);
        ApplyThemePalette(root, palette);
    }

    private void UpdateAppearanceColorPickers()
    {
        this.updatingAppearanceInputs = true;
        try
        {
            this.customAccentColorInput.Color = this.ResolveCustomAccentColor();
            this.customColorThemeInput.Color = this.ResolveCustomColorTheme();
            this.UpdateCustomColorInputVisibility();
        }
        finally
        {
            this.updatingAppearanceInputs = false;
        }
    }

    private void UpdateCustomColorInputVisibility()
    {
        this.customAccentColorField.Visibility = this.accentColorPreference == WindowsAccentColorPreference.Custom
            ? Visibility.Visible
            : Visibility.Collapsed;
        this.customColorThemeField.Visibility = this.colorThemePreference == WindowsColorThemePreference.Custom
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private Color ResolveCustomAccentColor()
    {
        return this.customAccentColor ?? DefaultCustomAccentColor();
    }

    private Color ResolveCustomColorTheme()
    {
        return this.customColorTheme ?? DefaultCustomColorTheme();
    }

    private static Color DefaultCustomAccentColor()
    {
        return WindowsThemePaletteResolver.ResolveAccentColor(WindowsAccentColorPreference.Blue, ElementTheme.Light);
    }

    private static Color DefaultCustomColorTheme()
    {
        return Color.FromArgb(255, 40, 80, 160);
    }

    private static Color? NormalizeOpaqueColor(Color? color)
    {
        return color is { } value
            ? NormalizeOpaqueColor(value)
            : null;
    }

    private static Color NormalizeOpaqueColor(Color color)
    {
        return Color.FromArgb(255, color.R, color.G, color.B);
    }

    private static void ApplyThemePalette(FrameworkElement root, WindowsThemePalette palette)
    {
        AppBackgroundBrush.Color = palette.AppBackgroundColor;
        CardBackgroundBrush.Color = palette.CardBackgroundColor;
        CardStrokeBrush.Color = palette.CardStrokeColor;
        LayerFillBrush.Color = palette.LayerFillColor;
        TextPrimaryBrush.Color = palette.TextPrimaryColor;
        TextSecondaryBrush.Color = palette.TextSecondaryColor;
        AccentBrush.Color = palette.AccentColor;
        AccentTextBrush.Color = palette.AccentTextColor;
        SuccessBrush.Color = palette.SuccessColor;
        CautionBrush.Color = palette.CautionColor;
        CriticalBrush.Color = palette.CriticalColor;

        root.Resources["SystemAccentColor"] = palette.AccentColor;
        root.Resources["AccentFillColorDefaultBrush"] = AccentBrush;
        root.Resources["AccentFillColorSecondaryBrush"] = AccentBrush;
        root.Resources["AccentFillColorTertiaryBrush"] = AccentBrush;
        root.Resources["AccentTextFillColorPrimaryBrush"] = AccentBrush;
        root.Resources["TextOnAccentFillColorPrimaryBrush"] = AccentTextBrush;
        root.Resources["NavigationViewSelectionIndicatorForeground"] = AccentBrush;
    }

    /// <summary>
    /// Resolves the current app theme palette so the tray flyout themes itself exactly like the shell.
    /// </summary>
    private WindowsThemePalette ResolveCurrentPalette()
    {
        var brightness = this.Content is FrameworkElement root
            ? ResolveBrushTheme(root, this.themePreference)
            : (this.themePreference == WindowsThemePreference.Dark ? ElementTheme.Dark : ElementTheme.Light);
        return WindowsThemePaletteResolver.Resolve(
            brightness,
            this.accentColorPreference,
            this.colorThemePreference,
            TryGetApplicationSystemAccentColor(),
            this.customAccentColor,
            this.customColorTheme);
    }

    private static Color? TryGetApplicationSystemAccentColor()
    {
        if (Application.Current?.Resources.TryGetValue("SystemAccentColor", out var resource) == true &&
            resource is Color color)
        {
            return color;
        }

        return null;
    }

    private UIElement BuildLogsPanel()
    {
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock
        {
            Text = "Logs and diagnostics",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        this.rawLogsText.TextWrapping = TextWrapping.Wrap;
        this.rawLogsText.FontFamily = new FontFamily("Consolas");
        this.rawLogsText.Foreground = ResourceBrush("TextFillColorPrimaryBrush");
        this.rawLogsText.Background = ResourceBrush("LayerFillColorDefaultBrush");
        this.rawLogsText.IsReadOnly = true;
        this.rawLogsText.AcceptsReturn = true;
        this.rawLogsText.MinHeight = 320;
        AutomationProperties.SetName(this.rawLogsText, "Raw log preview");

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        var refreshButton = new XamlButton
        {
            Content = "Refresh",
            AccessKey = "R",
            Command = this.CreateCommand(async () => await this.RefreshAllAsync()),
        };
        AutomationProperties.SetName(refreshButton, "Refresh logs and diagnostics");
        buttons.Children.Add(refreshButton);
        panel.Children.Add(buttons);
        panel.Children.Add(BuildDashboardCard("Diagnostics", this.logsDiagnosticsRows));
        panel.Children.Add(BuildDashboardCard("Locations", this.logsLocationCards));
        panel.Children.Add(this.BuildActivityHistoryCard());
        panel.Children.Add(this.BuildNotificationHistoryCard());
        panel.Children.Add(this.BuildSupportSummaryCard());
        panel.Children.Add(BuildDashboardCard("Gateway events", this.BuildChatEventVisibilityPanel()));
        panel.Children.Add(BuildDashboardCard("Filtered gateway events", this.chatEventMessages));
        panel.Children.Add(BuildDashboardCard("Raw log preview", this.rawLogsText));
        this.RenderLogsDiagnostics();
        return panel;
    }

    private UIElement BuildActivityHistoryCard()
    {
        var actions = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        actions.Children.Add(this.BuildSettingsActionButton(
            this.S("Shell.Logs.Activity.CopyButton", "Copy activity"),
            () => this.CopyActivityHistoryAsync()));
        actions.Children.Add(this.BuildSettingsActionButton(
            this.S("Shell.Logs.Activity.ClearButton", "Clear activity"),
            () => this.ClearActivityHistoryAsync()));
        return BuildDashboardCard(
            this.S("Shell.Logs.Activity.Title", "Recent activity history"),
            BuildSettingsSection(actions, this.logsActivityRows));
    }

    private UIElement BuildNotificationHistoryCard()
    {
        var actions = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        actions.Children.Add(this.BuildSettingsActionButton(
            this.S("Shell.Logs.Notifications.CopyButton", "Copy notifications"),
            () => this.CopyNotificationHistoryAsync()));
        actions.Children.Add(this.BuildSettingsActionButton(
            this.S("Shell.Logs.Notifications.ClearButton", "Clear notifications"),
            () => this.ClearNotificationHistoryAsync()));
        return BuildDashboardCard(
            this.S("Shell.Logs.Notifications.Title", "Notification history"),
            BuildSettingsSection(actions, this.logsNotificationRows));
    }

    private UIElement BuildSupportSummaryCard()
    {
        var actions = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        actions.Children.Add(this.BuildSettingsActionButton(
            this.S("Shell.Logs.Support.CopyButton", "Copy support summary"),
            () => this.CopySupportSummaryAsync()));
        actions.Children.Add(this.BuildSettingsActionButton(
            this.S("Shell.Logs.Support.SaveButton", "Save support artifact"),
            () => this.SaveSupportSummaryArtifactAsync()));
        return BuildDashboardCard(
            this.S("Shell.Logs.Support.Title", "Support summary"),
            BuildSettingsSection(actions, this.supportSummaryText));
    }

    private XamlButton ActionButton(string label, GatewayCliAction action)
    {
        return new XamlButton
        {
            Content = label,
            Command = this.CreateCommand(async () => await this.RunGatewayActionAsync(action)),
        };
    }

    private RelayCommand CreateCommand(Func<Task> execute)
    {
        return this.commandFactory.Create(execute);
    }

    private static ScrollViewer Scrollable(UIElement content)
    {
        var scrollViewer = new ScrollViewer
        {
            Content = AddScrollbarGutter(content),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        scrollViewer.SizeChanged += (_, args) => ResizeScrollableContent(scrollViewer, args.NewSize.Width);
        return scrollViewer;
    }

    private static Grid AddScrollbarGutter(UIElement content)
    {
        if (content is FrameworkElement element)
        {
            element.HorizontalAlignment = XamlHorizontalAlignment.Stretch;
        }

        return new Grid
        {
            Padding = new Thickness(0, 0, ScrollablePageScrollbarGutter, 0),
            HorizontalAlignment = XamlHorizontalAlignment.Stretch,
            Children =
            {
                content,
            },
        };
    }

    private static void ResizeScrollableContent(ScrollViewer scrollViewer, double width)
    {
        if (scrollViewer.Content is FrameworkElement content)
        {
            content.Width = Math.Max(0, width);
        }
    }

    private void ClearCommandError()
    {
        this.commandErrorText.Text = "";
        this.commandErrorText.Visibility = Visibility.Collapsed;
    }

    private void ReportCommandError(Exception ex)
    {
        CrashLog.Write(ex);
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            this.commandErrorText.Text = ex.Message;
            this.commandErrorText.Visibility = Visibility.Visible;
            this.detailText.Text = ex.Message;
            this.RenderLogsDiagnostics();
        });
    }

    private void OnRealtimeStateChanged(GatewayRealtimeState state, string? reason)
    {
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            this.coordinator.ApplyRealtimeState(state, reason);
            this.chatState.ApplyRealtimeState(state, reason);
            this.statusText.Text = $"Gateway: {state}";
            this.navigationGatewayStatusText.Text = $"Gateway: {state}";
            if (!string.IsNullOrWhiteSpace(reason))
            {
                this.detailText.Text = reason;
            }
            this.RenderHomeDashboard();
            this.RenderApprovals();
            this.RenderPairing();
            this.RenderSessions();
            this.RenderLogsDiagnostics();
            this.RenderChatWorkspace();
            _ = this.RecordActivityAsync(
                "realtime",
                $"Realtime {state}",
                string.IsNullOrWhiteSpace(reason) ? $"Gateway realtime state changed to {state}." : reason,
                WindowsNavigationDestination.Home);
            this.UpdateTrayTooltip();
        });
    }

    private void OnRealtimeEventReceived(GatewayRealtimeEvent @event)
    {
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            this.coordinator.RecordRealtimeEvent(@event);
            this.homeActivityText.Text = this.coordinator.LastActivity ?? "";
            SessionEventVisibility.AddBounded(this.chatRealtimeEvents, @event);
            this.sessionEventVisibility = this.sessionEventVisibility.WithObservedEvents(this.chatRealtimeEvents);
            this.RenderLogsDiagnostics();
            if (!string.Equals(@event.Name, "tick", StringComparison.OrdinalIgnoreCase))
            {
                _ = this.RecordActivityAsync(
                    "event",
                    $"Gateway event {@event.Name}",
                    $"Received gateway event {@event.Name}.",
                    WindowsNavigationDestination.Logs,
                    @event.Payload?.ToString());
            }
        });
    }

    private void OnCanvasNodeStateChanged(WindowsCanvasNodeState state, string? reason)
    {
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            if (state != WindowsCanvasNodeState.Connected)
            {
                this.ResetCanvasNavigationState();
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    CrashLog.WriteMessage($"Canvas node disconnected: {state} - {reason}");
                }
            }
            this.canvasStatusText.Text = $"Canvas node: {state}";
            if (!string.IsNullOrWhiteSpace(reason))
            {
                this.canvasDetailText.Text = reason;
            }
            this.RenderCanvasState();
            this.RenderDeviceCapabilityCards();
            _ = this.RecordActivityAsync(
                "canvas",
                $"Canvas node {state}",
                string.IsNullOrWhiteSpace(reason) ? $"Canvas node state changed to {state}." : reason,
                WindowsNavigationDestination.Canvas);
            this.UpdateTrayTooltip();
        });
    }

    private void OnTunnelStatusChanged(WindowsSshTunnelStatus status)
    {
        _ = this.DispatcherQueue.TryEnqueue(async () =>
        {
            this.tunnelStatusText.Text = status.LastError is { Length: > 0 } error
                ? $"Tunnel: {status.Summary}\n{error}"
                : $"Tunnel: {status.Summary}";
            var preferences = await this.appState.Preferences.LoadAsync();
            this.RenderTopologySnapshot(preferences);
            await this.RecordActivityAsync(
                "tunnel",
                status.Running ? "SSH tunnel started" : "SSH tunnel updated",
                status.LastError ?? status.Summary,
                WindowsNavigationDestination.Settings);
        });
    }

    private void OnCanvasSurfaceUrlChanged(string? a2uiHostUrl)
    {
        _ = this.DispatcherQueue.TryEnqueue(async () =>
        {
            if (!string.IsNullOrWhiteSpace(a2uiHostUrl))
            {
                await this.NavigateCanvasToA2UIAsync(a2uiHostUrl);
            }
            else
            {
                this.ResetCanvasNavigationState();
            }
            this.RenderCanvasState();
            this.RenderDeviceCapabilityCards();
        });
    }

    private async Task RefreshCanvasA2UIAsync(bool forceRefresh)
    {
        var a2uiUrl = this.appState.CanvasNode.A2UIHostUrl;
        if (forceRefresh || string.IsNullOrWhiteSpace(a2uiUrl))
        {
            await this.appState.CanvasNode.RefreshCanvasSurfaceUrlAsync();
            a2uiUrl = this.appState.CanvasNode.A2UIHostUrl;
        }

        if (string.IsNullOrWhiteSpace(a2uiUrl))
        {
            this.canvasStatusText.Text = "A2UI host unavailable";
            this.canvasDetailText.Text = "The gateway did not advertise the Canvas plugin surface.";
            return;
        }

        await this.NavigateCanvasToA2UIAsync(a2uiUrl);
    }

    private async Task NavigateCanvasToA2UIAsync(string a2uiUrl)
    {
        if (this.canvasWebView is null || this.appState.CanvasNode.State != WindowsCanvasNodeState.Connected)
        {
            return;
        }

        if (this.policyPreferences.BlockUnsafeUrls)
        {
            var evaluation = this.appState.UrlRisk.Evaluate(a2uiUrl);
            if (!evaluation.Allowed)
            {
                this.canvasStatusText.Text = "Blocked Canvas navigation";
                this.canvasDetailText.Text = evaluation.Reason ?? a2uiUrl;
                await this.RecordActivityAsync(
                    "canvas",
                    "Canvas navigation blocked",
                    this.canvasDetailText.Text,
                    WindowsNavigationDestination.Canvas,
                    a2uiUrl);
                return;
            }

            a2uiUrl = evaluation.NormalizedUrl ?? a2uiUrl;
        }

        this.canvasTrustedA2UIUrl = a2uiUrl;
        if (string.Equals(this.canvasNavigationTargetUrl, a2uiUrl, StringComparison.Ordinal) ||
            string.Equals(this.canvasLoadedA2UIUrl, a2uiUrl, StringComparison.Ordinal))
        {
            this.canvasWebView.Visibility = Visibility.Visible;
            return;
        }

        this.canvasNavigationTargetUrl = a2uiUrl;
        this.canvasLoadedA2UIUrl = null;
        this.canvasActiveNavigationId = null;
        this.canvasStatusText.Text = "Loading A2UI";
        this.canvasDetailText.Text = a2uiUrl;
        try
        {
            await this.EnsureCanvasBridgeAsync(this.canvasWebView);
            this.canvasWebView.Visibility = Visibility.Visible;
            this.canvasWebView.Source = new Uri(a2uiUrl);
        }
        catch (Exception ex)
        {
            this.canvasStatusText.Text = "A2UI load failed";
            this.canvasDetailText.Text = ex.Message;
            CrashLog.Write(ex);
        }
    }

    private void OnCanvasNavigationStarting(XamlWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (this.appState.CanvasNode.State != WindowsCanvasNodeState.Connected)
        {
            args.Cancel = true;
            this.ResetCanvasNavigationState();
            return;
        }

        if (this.policyPreferences.BlockUnsafeUrls)
        {
            var evaluation = this.appState.UrlRisk.Evaluate(args.Uri);
            if (!evaluation.Allowed)
            {
                args.Cancel = true;
                this.canvasActiveNavigationId = null;
                this.canvasStatusText.Text = "Blocked Canvas navigation";
                this.canvasDetailText.Text = evaluation.Reason ?? args.Uri;
                _ = this.RecordActivityAsync(
                    "canvas",
                    "Canvas navigation blocked",
                    this.canvasDetailText.Text,
                    WindowsNavigationDestination.Canvas,
                    args.Uri);
                return;
            }
        }

        if (WindowsCanvasA2UIUrl.IsTrustedA2UIUrl(args.Uri, this.canvasTrustedA2UIUrl))
        {
            this.canvasActiveNavigationId = args.NavigationId;
            return;
        }

        args.Cancel = true;
        this.canvasActiveNavigationId = null;
        this.canvasStatusText.Text = "Blocked Canvas navigation";
        this.canvasDetailText.Text = args.Uri;
    }

    private void OnCanvasNavigationCompleted(XamlWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (this.appState.CanvasNode.State != WindowsCanvasNodeState.Connected)
        {
            this.ResetCanvasNavigationState();
            return;
        }

        if (this.canvasActiveNavigationId is { } activeNavigationId &&
            args.NavigationId != activeNavigationId)
        {
            return;
        }

        var targetUrl = this.canvasNavigationTargetUrl;
        this.canvasNavigationTargetUrl = null;
        this.canvasActiveNavigationId = null;
        this.canvasStatusText.Text = args.IsSuccess ? "A2UI ready" : "A2UI navigation failed";
        if (args.IsSuccess)
        {
            this.canvasLoadedA2UIUrl = targetUrl ?? this.canvasTrustedA2UIUrl;
        }
        else
        {
            this.canvasLoadedA2UIUrl = null;
            this.canvasDetailText.Text = string.IsNullOrWhiteSpace(targetUrl)
                ? args.WebErrorStatus.ToString()
                : $"{args.WebErrorStatus}: {targetUrl}";
        }
    }

    private void ResetCanvasNavigationState()
    {
        this.canvasTrustedA2UIUrl = null;
        this.canvasNavigationTargetUrl = null;
        this.canvasLoadedA2UIUrl = null;
        this.canvasActiveNavigationId = null;
    }

    private async void OnCanvasCoreWebView2Initialized(XamlWebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        if (args.Exception is not null)
        {
            this.canvasStatusText.Text = "WebView2 initialization failed";
            this.canvasDetailText.Text = args.Exception.Message;
            CrashLog.Write(args.Exception);
            return;
        }

        sender.CoreWebView2.WebMessageReceived -= this.OnCanvasWebMessageReceived;
        sender.CoreWebView2.WebMessageReceived += this.OnCanvasWebMessageReceived;
        await this.EnsureCanvasBridgeAsync(sender);
    }

    private async void OnCanvasWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!WindowsCanvasA2UIUrl.IsTrustedA2UIUrl(args.Source, this.canvasTrustedA2UIUrl))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            if (!document.RootElement.TryGetProperty("userAction", out var action) ||
                action.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var name = ReadJsonString(action, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var preferences = await this.appState.Preferences.LoadAsync();
            await this.appState.Realtime.SendChatAsync(
                preferences.ChatSessionKey,
                $"A2UI action: {name}\n{action.GetRawText()}");
            this.canvasDetailText.Text = $"A2UI action sent to session {preferences.ChatSessionKey}.";
        }
        catch (Exception ex)
        {
            this.canvasDetailText.Text = $"A2UI action failed: {ex.Message}";
            CrashLog.Write(ex);
        }
    }

    private Task<WindowsCanvasInvokeResponse> HandleCanvasInvokeAsync(
        WindowsCanvasInvokeRequest request,
        CancellationToken cancellationToken)
    {
        return this.RunOnUiThreadAsync(async () =>
        {
            try
            {
                return request.Command switch
                {
                    WindowsCanvasCommands.Present => await this.HandleCanvasPresentAsync(),
                    WindowsCanvasCommands.Hide => this.HandleCanvasHide(),
                    WindowsCanvasCommands.Navigate => await this.HandleCanvasNavigateAsync(request.ParamsJson),
                    WindowsCanvasCommands.Eval => await this.HandleCanvasEvalAsync(request.ParamsJson),
                    WindowsCanvasCommands.Snapshot => WindowsCanvasInvokeResponse.Failure(
                        "UNAVAILABLE",
                        "canvas.snapshot is not implemented in the Windows companion yet."),
                    WindowsCanvasCommands.A2UIReset => await this.HandleCanvasA2UIResetAsync(),
                    WindowsCanvasCommands.A2UIPush or WindowsCanvasCommands.A2UIPushJsonl =>
                        await this.HandleCanvasA2UIPushAsync(request.ParamsJson),
                    _ => WindowsCanvasInvokeResponse.Failure("INVALID_REQUEST", $"Unknown Canvas command: {request.Command}"),
                };
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
                return WindowsCanvasInvokeResponse.Failure("UNAVAILABLE", ex.Message);
            }
        }, cancellationToken);
    }

    private async Task<WindowsCanvasInvokeResponse> HandleCanvasPresentAsync()
    {
        await this.RefreshCanvasA2UIAsync(forceRefresh: false);
        return WindowsCanvasInvokeResponse.Success("""{"status":"shown"}""");
    }

    private WindowsCanvasInvokeResponse HandleCanvasHide()
    {
        if (this.canvasWebView is not null)
        {
            this.canvasWebView.Visibility = Visibility.Collapsed;
        }
        this.canvasStatusText.Text = "Canvas hidden";
        return WindowsCanvasInvokeResponse.Success("""{"status":"hidden"}""");
    }

    private async Task<WindowsCanvasInvokeResponse> HandleCanvasNavigateAsync(string? paramsJson)
    {
        var target = ReadParamString(paramsJson, "url") ?? ReadParamString(paramsJson, "path");
        if (string.IsNullOrWhiteSpace(target))
        {
            await this.RefreshCanvasA2UIAsync(forceRefresh: false);
            return WindowsCanvasInvokeResponse.Success("""{"status":"shown"}""");
        }

        if (this.policyPreferences.BlockUnsafeUrls)
        {
            var evaluation = this.appState.UrlRisk.Evaluate(target);
            if (!evaluation.Allowed)
            {
                return WindowsCanvasInvokeResponse.Failure(
                    "INVALID_REQUEST",
                    evaluation.Reason ?? "The requested Canvas URL was blocked by policy.");
            }

            target = evaluation.NormalizedUrl ?? target;
        }

        if (WindowsCanvasA2UIUrl.IsTrustedA2UIUrl(target, this.canvasTrustedA2UIUrl))
        {
            await this.NavigateCanvasToA2UIAsync(target);
            return WindowsCanvasInvokeResponse.Success("""{"status":"web"}""");
        }

        return WindowsCanvasInvokeResponse.Failure(
            "INVALID_REQUEST",
            "Windows Canvas currently allows navigation only to the trusted gateway A2UI host.");
    }

    private async Task<WindowsCanvasInvokeResponse> HandleCanvasEvalAsync(string? paramsJson)
    {
        var javaScript = ReadParamString(paramsJson, "javaScript") ?? ReadParamString(paramsJson, "js");
        if (string.IsNullOrWhiteSpace(javaScript))
        {
            return WindowsCanvasInvokeResponse.Failure("INVALID_REQUEST", "canvas.eval requires javaScript.");
        }

        var result = await this.ExecuteCanvasScriptAsync(javaScript);
        return WindowsCanvasInvokeResponse.Success(JsonSerializer.Serialize(new { result }));
    }

    private async Task<WindowsCanvasInvokeResponse> HandleCanvasA2UIResetAsync()
    {
        await this.EnsureCanvasA2UIReadyAsync();
        var result = await this.ExecuteCanvasScriptAsync(
            """
            (() => {
              const host = globalThis.openclawA2UI;
              if (!host) return JSON.stringify({ ok: false, error: "missing openclawA2UI" });
              const result = host.reset();
              return JSON.stringify(result);
            })()
            """);
        this.canvasDetailText.Text = $"A2UI reset: {result}";
        return WindowsCanvasInvokeResponse.Success(result);
    }

    private async Task<WindowsCanvasInvokeResponse> HandleCanvasA2UIPushAsync(string? paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramsJson))
        {
            return WindowsCanvasInvokeResponse.Failure("INVALID_REQUEST", "canvas.a2ui.push requires params.");
        }

        using var document = JsonDocument.Parse(paramsJson);
        var root = document.RootElement;
        string messagesJson;
        if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
        {
            messagesJson = messages.GetRawText();
        }
        else if (root.TryGetProperty("jsonl", out var jsonl) && jsonl.ValueKind == JsonValueKind.String)
        {
            var decoded = WindowsCanvasA2UIJsonl.DecodeMessagesFromJsonl(jsonl.GetString() ?? "");
            messagesJson = JsonSerializer.Serialize(decoded.Select(message => message.RootElement));
        }
        else
        {
            return WindowsCanvasInvokeResponse.Failure(
                "INVALID_REQUEST",
                "canvas.a2ui.push requires messages or jsonl.");
        }

        await this.EnsureCanvasA2UIReadyAsync();
        var result = await this.ExecuteCanvasScriptAsync(
            $$"""
            (() => {
              try {
                const host = globalThis.openclawA2UI;
                if (!host) return JSON.stringify({ ok: false, error: "missing openclawA2UI" });
                const messages = {{messagesJson}};
                const result = host.applyMessages(messages);
                const element = document.querySelector("openclaw-a2ui-host");
                const surfaces = Array.isArray(result?.surfaces)
                  ? result.surfaces
                  : Array.isArray(element?.surfaces)
                    ? element.surfaces.map(([id]) => id)
                    : [];
                return JSON.stringify({
                  ...result,
                  surfaces,
                });
              } catch (e) {
                return JSON.stringify({ ok: false, error: String(e?.message ?? e) });
              }
            })()
            """);
        var rendererResult = WindowsCanvasA2UI.ParseRendererResult(result);
        this.canvasDetailText.Text = rendererResult?.Surfaces.Count > 0
            ? $"A2UI updated: {string.Join(", ", rendererResult.Surfaces)}"
            : "A2UI updated.";
        if (rendererResult is { Rejected: true })
        {
            return WindowsCanvasInvokeResponse.Failure(
                "UNAVAILABLE",
                rendererResult.Error ?? $"A2UI renderer rejected the push: {result}");
        }
        return WindowsCanvasInvokeResponse.Success(result);
    }

    private async Task EnsureCanvasA2UIReadyAsync()
    {
        this.ShowDestination(WindowsNavigationDestination.Canvas);
        if (await this.IsCanvasA2UIReadyAsync())
        {
            return;
        }

        await this.RefreshCanvasA2UIAsync(forceRefresh: string.IsNullOrWhiteSpace(this.appState.CanvasNode.A2UIHostUrl));
        var deadline = DateTimeOffset.UtcNow.AddSeconds(6);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await this.IsCanvasA2UIReadyAsync())
            {
                return;
            }
            await Task.Delay(120);
        }

        throw new InvalidOperationException("A2UI_HOST_UNAVAILABLE: A2UI host not reachable.");
    }

    private async Task<bool> IsCanvasA2UIReadyAsync()
    {
        try
        {
            var result = await this.ExecuteCanvasScriptAsync(
                "(() => String(Boolean(globalThis.openclawA2UI)))()");
            return string.Equals(result.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> ExecuteCanvasScriptAsync(string javaScript)
    {
        if (this.canvasWebView is null)
        {
            throw new InvalidOperationException("Canvas WebView is not ready.");
        }

        await this.EnsureCanvasBridgeAsync(this.canvasWebView);
        var raw = await this.canvasWebView.ExecuteScriptAsync(javaScript);
        return WindowsCanvasA2UI.DecodeExecuteScriptResult(raw);
    }

    private async Task EnsureCanvasBridgeAsync(XamlWebView2 webView)
    {
        await webView.EnsureCoreWebView2Async();
        if (webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException("Canvas WebView2 runtime is not ready.");
        }

        webView.CoreWebView2.WebMessageReceived -= this.OnCanvasWebMessageReceived;
        webView.CoreWebView2.WebMessageReceived += this.OnCanvasWebMessageReceived;
        if (this.canvasBridgeScriptInstalled)
        {
            return;
        }

        await this.canvasBridgeScriptGate.WaitAsync();
        try
        {
            if (this.canvasBridgeScriptInstalled)
            {
                return;
            }

            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(CanvasBridgeScript());
            this.canvasBridgeScriptInstalled = true;
        }
        finally
        {
            this.canvasBridgeScriptGate.Release();
        }
    }

    private Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!this.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    completion.TrySetResult(await action());
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }))
        {
            completion.TrySetException(new InvalidOperationException("WinUI dispatcher is unavailable."));
        }
        return completion.Task.WaitAsync(cancellationToken);
    }

    private void RenderCanvasState()
    {
        if (!this.canvasNodeEnabled)
        {
            this.canvasStatusText.Text = "Canvas node disabled";
            this.canvasDetailText.Text = "Enable Canvas and A2UI node in Settings.";
            return;
        }

        if (this.appState.CanvasNode.State == WindowsCanvasNodeState.Connected)
        {
            this.canvasStatusText.Text = string.IsNullOrWhiteSpace(this.appState.CanvasNode.A2UIHostUrl)
                ? "Canvas node connected"
                : "A2UI host available";
            this.canvasDetailText.Text = this.appState.CanvasNode.A2UIHostUrl ??
                "Connected. The gateway has not advertised a Canvas plugin surface yet.";
            return;
        }

        this.canvasStatusText.Text = $"Canvas node: {this.appState.CanvasNode.State}";
        if (string.IsNullOrWhiteSpace(this.canvasDetailText.Text))
        {
            this.canvasDetailText.Text = "Connect to the gateway to expose the Windows A2UI Canvas node.";
        }
    }

    private static string CanvasBridgeScript()
    {
        return """
        (() => {
          try {
            if (globalThis.__openclawWindowsA2UIBridgeInstalled) return;
            globalThis.__openclawWindowsA2UIBridgeInstalled = true;
            globalThis.openclawCanvasA2UIAction = {
              postMessage: (message) => {
                try {
                  const payload = typeof message === 'string' ? JSON.parse(message) : message;
                  globalThis.chrome?.webview?.postMessage(payload);
                } catch {
                  globalThis.chrome?.webview?.postMessage(message);
                }
              },
            };
            globalThis.addEventListener('a2uiaction', (evt) => {
              try {
                const payload = evt?.detail ?? evt?.payload ?? null;
                if (!payload || payload.eventType !== 'a2ui.action') return;
                const action = payload.action ?? null;
                const name = action?.name ?? '';
                if (!name) return;
                const context = Array.isArray(action?.context) ? action.context : [];
                globalThis.chrome?.webview?.postMessage({
                  userAction: {
                    id: globalThis.crypto?.randomUUID?.() ?? String(Date.now()),
                    name,
                    surfaceId: payload.surfaceId ?? 'main',
                    sourceComponentId: payload.sourceComponentId ?? '',
                    dataContextPath: payload.dataContextPath ?? '',
                    timestamp: new Date().toISOString(),
                    ...(context.length ? { context } : {}),
                  },
                });
              } catch {}
            }, true);
          } catch {}
        })();
        """;
    }

    private static string? ReadParamString(string? paramsJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(paramsJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(paramsJson);
        return ReadJsonString(document.RootElement, propertyName);
    }

    private static string? ReadJsonString(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (this.exitRequested || this.trayHost is null)
        {
            return;
        }

        args.Cancel = true;
        sender.Hide();
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        await this.ShutdownAsync();
    }

    private async Task ShutdownAsync()
    {
        this.AppWindow.Closing -= this.OnAppWindowClosing;
        if (this.shutdownStarted)
        {
            return;
        }

        this.shutdownStarted = true;
        this.appState.Realtime.StateChanged -= this.OnRealtimeStateChanged;
        this.appState.Realtime.EventReceived -= this.OnRealtimeEventReceived;
        this.appState.CanvasNode.StateChanged -= this.OnCanvasNodeStateChanged;
        this.appState.CanvasNode.CanvasSurfaceUrlChanged -= this.OnCanvasSurfaceUrlChanged;
        this.appState.CanvasNode.InvokeAsync = null;
        this.appState.Tunnel.StatusChanged -= this.OnTunnelStatusChanged;
        if (this.canvasWebView?.CoreWebView2 is not null)
        {
            this.canvasWebView.CoreWebView2.WebMessageReceived -= this.OnCanvasWebMessageReceived;
        }
        this.canvasWebView?.Close();
        this.hotkeyService?.Dispose();
        this.trayFlyout?.RequestClose();
        this.overlayWindow?.Close();
        this.screenRecordingCancellation?.Cancel();
        this.screenRecordingCancellation?.Dispose();
        this.screenRecordingCancellation = null;
        this.appState.Tunnel.Dispose();
        try
        {
            await this.appState.CanvasNode.DisposeAsync();
            await this.appState.Realtime.DisposeAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
        }
    }

    private async Task RefreshAllAsync()
    {
        // Keep refresh ordering explicit because later panels depend on status, onboarding, and preferences snapshots.
        try
        {
            try
            {
                var status = await this.coordinator.RefreshGatewayStatusAsync();
                this.RenderStatus(status);
            }
            catch (Exception ex)
            {
                this.statusText.Text = "Gateway status unavailable";
                this.detailText.Text = ex.Message;
                this.coordinator.ClearGatewayStatus(ex);
                this.RenderHomeDashboard();
                this.RenderLogsDiagnostics();
            }

            var checks = await this.coordinator.RefreshOnboardingAsync();
            this.onboardingList.Children.Clear();
            foreach (var check in checks)
            {
                this.onboardingList.Children.Add(BuildOnboardingRow(check));
            }
            this.RenderHomeDashboard();

            var preferences = await this.appState.Preferences.LoadAsync();
            this.currentPreferences = preferences;
            this.lastGatewayStatusCheckedAt = preferences.LastStatusCheckedAt;
            this.gatewayUrlInput.Text = preferences.GatewayUrl;
            this.gatewayTokenInput.Password = preferences.GatewayToken ?? "";
            this.chatSessionInput.Text = preferences.ChatSessionKey;
            this.openMainWindowOnLaunch = preferences.OpenMainWindowOnLaunch;
            this.ApplyAppearancePreferences(
                preferences.ThemePreference,
                preferences.AccentColorPreference,
                preferences.CustomAccentColor,
                preferences.ColorThemePreference,
                preferences.CustomColorTheme);
            this.sessionEventVisibility = preferences.SessionEventVisibility.WithObservedEvents(this.chatRealtimeEvents);
            this.notificationPreferences = preferences.NotificationPreferences;
            this.notificationRulePreferences = WindowsNotificationRuleEvaluator.NormalizePreferences(preferences.NotificationRules);
            this.topologyPreferences = preferences.Topology;
            this.diagnosticsPreferences = preferences.Diagnostics;
            this.policyPreferences = preferences.Policy;
            this.canvasNodeEnabled = preferences.CanvasNodeEnabled;
            this.voiceControlsEnabled = preferences.VoiceControlsEnabled;
            this.globalHotkeyEnabled = preferences.GlobalHotkeyEnabled;
            this.tunnelHostInput.Text = preferences.Topology.SshHost;
            this.tunnelRemoteHostInput.Text = preferences.Topology.RemoteHost;
            this.tunnelLocalPortInput.Text = preferences.Topology.LocalPort.ToString(CultureInfo.InvariantCulture);
            this.tunnelRemotePortInput.Text = preferences.Topology.RemotePort.ToString(CultureInfo.InvariantCulture);
            this.diagnosticsPathInput.Text = preferences.Diagnostics.StructuredDiagnosticsPath;
            this.activityRetentionCountInput.Text = preferences.Diagnostics.ActivityRetentionCount.ToString(CultureInfo.InvariantCulture);
            this.openMainWindowOnLaunchInput.IsChecked = preferences.OpenMainWindowOnLaunch;
            this.PopulateAppearancePreviewItems(this.ResolveCurrentBrushTheme());
            this.SelectThemePreference(preferences.ThemePreference);
            this.SelectAccentColor(preferences.AccentColorPreference);
            this.SelectColorTheme(preferences.ColorThemePreference);
            this.SelectApprovalPolicy(preferences.Policy.ApprovalPolicy);
            this.approvalAlertsInput.IsChecked = preferences.NotificationPreferences.ApprovalAlerts;
            this.pairingAlertsInput.IsChecked = preferences.NotificationPreferences.PairingAlerts;
            this.gatewayHealthAlertsInput.IsChecked = preferences.NotificationPreferences.GatewayHealthAlerts;
            this.devicePermissionAlertsInput.IsChecked = preferences.NotificationPreferences.DevicePermissionAlerts;
            this.PopulateNotificationRuleEditor(this.notificationRulePreferences);
            this.tunnelAutoStartInput.IsChecked = preferences.Topology.AutoStartTunnel;
            this.structuredDiagnosticsEnabledInput.IsChecked = preferences.Diagnostics.StructuredDiagnosticsEnabled;
            this.blockUnsafeUrlsInput.IsChecked = preferences.Policy.BlockUnsafeUrls;
            this.redactSensitiveContentInput.IsChecked = preferences.Policy.RedactSensitiveContent;
            this.canvasNodeEnabledInput.IsChecked = preferences.CanvasNodeEnabled;
            this.settingsVoiceControlsInput.IsChecked = preferences.VoiceControlsEnabled;
            this.settingsGlobalHotkeyInput.IsChecked = preferences.GlobalHotkeyEnabled;
            await this.appState.Tunnel.ApplyPreferencesAsync(preferences.Topology);
            this.RenderHomeDashboard();
            this.RenderSettingsSummary(preferences);
            this.RenderSettingsStorage();
            this.RenderLogsDiagnostics();
            this.RenderTopologySnapshot(preferences);
            this.RenderCanvasState();
            this.RenderActivityHistory();
            this.RenderNotificationActivity();
            await this.RefreshDeviceCapabilitiesAsync();
            if (this.appState.Realtime.State == GatewayRealtimeState.Connected)
            {
                await this.RefreshSessionsAsync();
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            this.coordinator.RecordRefreshFailure(ex);
            this.statusText.Text = "Startup refresh failed";
            this.detailText.Text = ex.Message;
            this.homeActivityText.Text = this.coordinator.LastActivity ?? "";
            this.RenderLogsDiagnostics();
        }
    }

    private async Task ConnectRealtimeAsync()
    {
        await this.SaveSettingsAsync();
        await this.appState.Realtime.ReconnectAsync();
        await this.RecordActivityAsync("gateway", "Realtime connected", "Connected the Windows companion realtime channel.");
        await this.ConnectCanvasNodeAsync();
        await this.RefreshChatAsync();
        await this.RefreshSessionsAsync();
        await this.RefreshApprovalsAsync();
        await this.RefreshPairingAsync();
    }

    private async Task ConnectCanvasNodeAsync()
    {
        var preferences = await this.appState.Preferences.LoadAsync();
        if (!preferences.CanvasNodeEnabled)
        {
            this.canvasStatusText.Text = "Canvas node disabled";
            this.canvasDetailText.Text = "Enable Canvas and A2UI node in Settings to expose A2UI to the gateway.";
            await this.appState.CanvasNode.DisconnectAsync();
            return;
        }

        this.canvasStatusText.Text = "Connecting Canvas node...";
        try
        {
            await this.appState.CanvasNode.ReconnectAsync();
            await this.RefreshCanvasA2UIAsync(forceRefresh: false);
        }
        catch (Exception ex)
        {
            this.canvasStatusText.Text = "Canvas node connection failed";
            this.canvasDetailText.Text = ex.Message;
            CrashLog.Write(ex);
        }
    }

    private async Task RefreshChatAsync()
    {
        try
        {
            var preferences = await this.appState.Preferences.LoadAsync();
            var messages = await this.appState.Realtime.LoadChatHistoryAsync(preferences.ChatSessionKey);
            this.chatState.ApplyMessages(messages, this.appState.Realtime.State);
            this.chatScrollToBottomRequested = true;
            this.RenderChatWorkspace(preferences.ChatSessionKey);
        }
        catch (Exception ex)
        {
            this.chatState.ApplyFailure(ex);
            this.RenderChatWorkspace();
            throw;
        }
    }

    private async Task RefreshSessionsAsync()
    {
        try
        {
            this.sessionsStatusText.Text = "Loading sessions...";
            this.latestSessionsError = null;
            this.latestSessions = await this.appState.Realtime.ListSessionsAsync();
            this.RenderSessions();
        }
        catch (Exception ex)
        {
            this.latestSessionsError = ex.Message;
            this.latestSessions = [];
            this.RenderSessions();
        }
    }

    private void OnChatInputKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (!ChatComposerKeyboard.IsSendShortcut(args.Key, IsControlKeyDown()))
        {
            return;
        }

        args.Handled = true;
        this.chatSendButton.Command?.Execute(null);
    }

    private static bool IsControlKeyDown()
    {
        return InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(global::Windows.UI.Core.CoreVirtualKeyStates.Down);
    }

    private async Task SendChatAsync()
    {
        var message = this.chatInput.Text.Trim();
        if (message.Length == 0 || this.chatState.Status == ChatWorkspaceStatus.Sending)
        {
            return;
        }
        this.chatState.StartSending();
        this.RenderChatWorkspace();
        try
        {
            var preferences = await this.appState.Preferences.LoadAsync();
            await this.appState.Realtime.SendChatAsync(preferences.ChatSessionKey, message);
            this.chatInput.Text = "";
            await this.RecordActivityAsync("chat", "Chat message sent", $"Sent a message to session {preferences.ChatSessionKey}.", WindowsNavigationDestination.Chat, message);
            await this.RefreshChatAsync();
        }
        catch (Exception ex)
        {
            this.chatState.ApplyFailure(ex);
            this.RenderChatWorkspace();
            throw;
        }
    }

    private void RenderLogsDiagnostics()
    {
        var summary = LogsDiagnosticsSummary.Create(
            CrashLog.Path,
            this.coordinator.GatewayStatus,
            this.coordinator.LastError,
            this.lastGatewayStatusCheckedAt);
        this.logsDiagnosticsRows.Children.Clear();
        this.logsDiagnosticsRows.Children.Add(BuildDashboardRow("Gateway", summary.GatewayStatus));
        this.logsDiagnosticsRows.Children.Add(BuildDashboardRow("Last error", summary.LastError));
        this.logsDiagnosticsRows.Children.Add(BuildDashboardRow("Last refresh", summary.LastRefresh));
        this.logsDiagnosticsRows.Children.Add(BuildStackedDashboardRow(
            "Structured diagnostics",
            this.appState.Diagnostics.ResolvePath(this.diagnosticsPreferences.StructuredDiagnosticsPath)));
        this.logsDiagnosticsRows.Children.Add(BuildStackedDashboardRow("Activity history", this.appState.ActivityHistory.Path));
        this.logsDiagnosticsRows.Children.Add(BuildStackedDashboardRow("Notification history", this.appState.NotificationHistory.Path));

        this.logsLocationCards.Children.Clear();
        this.logsLocationCards.Children.Add(this.BuildLogLocationCard(
            "App crash log",
            summary.AppLogPath,
            summary.AppLogFolderPath,
            "Unhandled Windows companion exceptions are appended here.",
            summary.CanUseAppLogActions));
        this.logsLocationCards.Children.Add(this.BuildLogLocationCard(
            "Gateway log",
            summary.GatewayLogPath,
            summary.GatewayLogFolderPath,
            "Gateway lifecycle and service logs reported by the CLI.",
            summary.CanUseGatewayLogActions));

        this.rawLogsText.Text = BuildRawLogPreview(summary);
        this.logsText.Text =
            $"App logs: {summary.AppLogPath}\n" +
            $"Gateway logs: {summary.GatewayLogPath}\n" +
            $"Structured diagnostics: {this.appState.Diagnostics.ResolvePath(this.diagnosticsPreferences.StructuredDiagnosticsPath)}\n" +
            $"Activity history: {this.appState.ActivityHistory.Path}\n" +
            $"Notification history: {this.appState.NotificationHistory.Path}\n" +
            $"Gateway status: {summary.GatewayStatus}\n" +
            $"Last error: {summary.LastError}\n" +
            $"Last refresh: {summary.LastRefresh}";
        this.RenderActivityHistory();
        this.RenderNotificationHistory();
        this.RenderSupportSummarySnapshot();
        this.RenderGatewayEvents();
    }

    private UIElement BuildLogLocationCard(
        string title,
        string path,
        string folderPath,
        string detail,
        bool canUseActions)
    {
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        body.Children.Add(new TextBlock
        {
            Text = detail,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });
        body.Children.Add(BuildDashboardRow("Path", path));

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(this.LogActionButton("Copy path", $"Copy {title} path", canUseActions, () =>
        {
            CopyTextToClipboard(path);
            this.rawLogsText.Text = $"{title} path copied.\n\n{BuildRawLogPreview(CurrentLogsSummary())}";
        }));
        buttons.Children.Add(this.LogActionButton("Reveal file", $"Reveal {title} file", canUseActions, () => RevealLogFile(path)));
        buttons.Children.Add(this.LogActionButton("Open folder", $"Open {title} folder", canUseActions, () => OpenLogFolder(folderPath)));
        body.Children.Add(buttons);
        return BuildDashboardCard(null, body);
    }

    private XamlButton LogActionButton(string label, string automationName, bool isEnabled, Action execute)
    {
        var button = new XamlButton
        {
            Content = label,
            IsEnabled = isEnabled,
            Command = this.CreateCommand(() =>
            {
                execute();
                return Task.CompletedTask;
            }),
        };
        AutomationProperties.SetName(button, automationName);
        return button;
    }

    private LogsDiagnosticsSummary CurrentLogsSummary()
    {
        return LogsDiagnosticsSummary.Create(
            CrashLog.Path,
            this.coordinator.GatewayStatus,
            this.coordinator.LastError,
            this.lastGatewayStatusCheckedAt);
    }

    private static void CopyTextToClipboard(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text == "unknown")
        {
            return;
        }
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private static void RevealLogFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "unknown")
        {
            return;
        }
        WindowsShell.OpenFileInExplorer(path);
    }

    private static void OpenLogFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "unknown")
        {
            return;
        }
        WindowsShell.OpenFileInExplorer(path);
    }

    private static string BuildRawLogPreview(LogsDiagnosticsSummary summary)
    {
        var lines = new List<string>
        {
            $"app_log={summary.AppLogPath}",
            $"gateway_log={summary.GatewayLogPath}",
            $"gateway_status={summary.GatewayStatus}",
            $"last_error={summary.LastError}",
            $"last_refresh={summary.LastRefresh}",
            "",
            "[app crash log]",
            ReadLogPreview(summary.AppLogPath),
            "",
            "[gateway log]",
            ReadLogPreview(summary.GatewayLogPath),
        };
        return string.Join(Environment.NewLine, lines);
    }

    private static string ReadLogPreview(string path)
    {
        const int maxPreviewLength = 6000;
        if (string.IsNullOrWhiteSpace(path) || path == "unknown")
        {
            return "No log path is available.";
        }
        if (!File.Exists(path))
        {
            return "Log file does not exist yet.";
        }

        try
        {
            var text = File.ReadAllText(path);
            if (text.Length <= maxPreviewLength)
            {
                return text.Length == 0 ? "Log file is empty." : text;
            }
            return text[^maxPreviewLength..];
        }
        catch (Exception ex)
        {
            return $"Unable to read log file: {ex.Message}";
        }
    }

    private void RenderChatWorkspace(string? sessionKey = null)
    {
        var activeSession = this.GetActiveChatSessionKey(sessionKey);
        this.chatSessionText.Text =
            this.chatState.LastLoadedAt is { } lastLoadedAt
                ? $"Session {activeSession} · updated {lastLoadedAt.ToLocalTime():g}"
                : $"Session {activeSession}";
        this.chatStateText.Text = $"{this.chatState.Status}: {this.chatState.StatusDetail ?? "No detail available."}";
        this.chatStateText.Foreground = this.chatState.Status switch
        {
            ChatWorkspaceStatus.Connected => ResourceBrush("SystemFillColorSuccessBrush"),
            ChatWorkspaceStatus.Sending => ResourceBrush("TextFillColorPrimaryBrush"),
            ChatWorkspaceStatus.Failed => ResourceBrush("SystemFillColorCriticalBrush"),
            ChatWorkspaceStatus.Disconnected => ResourceBrush("SystemFillColorCautionBrush"),
            _ => ResourceBrush("TextFillColorSecondaryBrush"),
        };
        this.chatSendButton.IsEnabled = this.chatState.Status != ChatWorkspaceStatus.Sending;
        this.chatRefreshButton.IsEnabled = this.chatState.Status != ChatWorkspaceStatus.Sending;
        this.chatComposerRefreshButton.IsEnabled = this.chatState.Status != ChatWorkspaceStatus.Sending;

        this.chatEmptyText.Text = "No messages in this session yet.";
        this.chatEmptyText.Visibility =
            this.chatState.Messages.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        var priorOffset = this.chatTranscriptScrollViewer?.VerticalOffset;
        this.chatMessages.Children.Clear();
        foreach (var message in this.chatState.Messages)
        {
            this.chatMessages.Children.Add(BuildChatMessageRow(message));
        }

        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            if (this.chatTranscriptScrollViewer is null)
            {
                return;
            }

            var targetOffset = this.chatScrollToBottomRequested
                ? this.chatTranscriptScrollViewer.ScrollableHeight
                : Math.Min(priorOffset ?? 0, this.chatTranscriptScrollViewer.ScrollableHeight);
            this.chatTranscriptScrollViewer.ChangeView(null, targetOffset, null, true);
            this.chatScrollToBottomRequested = false;
        });
    }

    private void RenderGatewayEvents()
    {
        var visibleEvents = SessionEventVisibility.Filter(
            this.chatRealtimeEvents,
            this.sessionEventVisibility,
            activeSession: null);
        var hiddenEventCount = SessionEventVisibility.CountHidden(
            this.chatRealtimeEvents,
            this.sessionEventVisibility,
            activeSession: null);
        this.chatEventMessages.Children.Clear();
        if (visibleEvents.Count == 0)
        {
            this.chatEventMessages.Children.Add(new TextBlock
            {
                Text = hiddenEventCount > 0
                    ? $"No visible gateway events match the current filters. {hiddenEventCount} hidden event{Plural(hiddenEventCount)} can be restored."
                    : "No gateway events captured yet.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
            });
        }
        foreach (var @event in visibleEvents)
        {
            this.chatEventMessages.Children.Add(BuildChatEventRow(@event));
        }
        this.RenderChatEventVisibilityControls("all sessions", hiddenEventCount);
    }

    private void RenderChatEventVisibilityControls(string activeSession, int hiddenEventCount)
    {
        var eventTypes = SessionEventVisibility.EventTypesForControls(
            this.chatRealtimeEvents,
            this.sessionEventVisibility);
        this.chatEventVisibilitySummaryText.Text =
            $"{eventTypes.Count} event type{Plural(eventTypes.Count)} available. " +
            $"{hiddenEventCount} hidden for session {activeSession}.";
        var signature = string.Join("\n", eventTypes);
        if (string.Equals(this.chatEventVisibilityControlSignature, signature, StringComparison.Ordinal))
        {
            this.SyncChatEventVisibilityControlState();
            return;
        }

        this.chatEventVisibilityControlSignature = signature;
        this.chatEventVisibilityControls.Children.Clear();
        foreach (var eventType in eventTypes)
        {
            var checkBox = new XamlCheckBox
            {
                Content = EventVisibilityLabel(eventType),
                IsChecked = this.sessionEventVisibility.IsVisible(eventType),
                Tag = eventType,
            };
            ApplyAccentCheckedState(checkBox);
            AutomationProperties.SetName(checkBox, $"Show {eventType} events");
            checkBox.Checked += this.OnSessionEventVisibilityChecked;
            checkBox.Unchecked += this.OnSessionEventVisibilityUnchecked;
            this.chatEventVisibilityControls.Children.Add(checkBox);
        }
    }

    private async void OnSessionEventVisibilityChecked(object sender, RoutedEventArgs args)
    {
        if (this.updatingSessionEventVisibilityControls)
        {
            return;
        }

        if (sender is XamlCheckBox { Tag: string eventType })
        {
            await this.SetSessionEventVisibilityOrReportAsync(eventType, visible: true);
        }
    }

    private async void OnSessionEventVisibilityUnchecked(object sender, RoutedEventArgs args)
    {
        if (this.updatingSessionEventVisibilityControls)
        {
            return;
        }

        if (sender is XamlCheckBox { Tag: string eventType })
        {
            await this.SetSessionEventVisibilityOrReportAsync(eventType, visible: false);
        }
    }

    private void SyncChatEventVisibilityControlState()
    {
        this.updatingSessionEventVisibilityControls = true;
        try
        {
            foreach (var checkBox in this.chatEventVisibilityControls.Children.OfType<XamlCheckBox>())
            {
                ApplyAccentCheckedState(checkBox);
                if (checkBox.Tag is string eventType)
                {
                    checkBox.IsChecked = this.sessionEventVisibility.IsVisible(eventType);
                }
            }
        }
        finally
        {
            this.updatingSessionEventVisibilityControls = false;
        }
    }

    private static void ApplyAccentCheckedState(XamlCheckBox checkBox)
    {
        checkBox.Resources["CheckBoxCheckBackgroundFillChecked"] = AccentBrush;
        checkBox.Resources["CheckBoxCheckBackgroundFillCheckedPointerOver"] = AccentBrush;
        checkBox.Resources["CheckBoxCheckBackgroundFillCheckedPressed"] = AccentBrush;
        checkBox.Resources["CheckBoxCheckBackgroundStrokeChecked"] = AccentBrush;
        checkBox.Resources["CheckBoxCheckBackgroundStrokeCheckedPointerOver"] = AccentBrush;
        checkBox.Resources["CheckBoxCheckBackgroundStrokeCheckedPressed"] = AccentBrush;
        checkBox.Resources["CheckBoxCheckGlyphForegroundChecked"] = AccentTextBrush;
        checkBox.Resources["CheckBoxCheckGlyphForegroundCheckedPointerOver"] = AccentTextBrush;
        checkBox.Resources["CheckBoxCheckGlyphForegroundCheckedPressed"] = AccentTextBrush;
    }

    private async Task SetSessionEventVisibilityOrReportAsync(string eventType, bool visible)
    {
        try
        {
            await this.SetSessionEventVisibilityAsync(eventType, visible);
        }
        catch (Exception ex)
        {
            this.ReportCommandError(ex);
        }
    }

    private async Task SetSessionEventVisibilityAsync(string eventType, bool visible)
    {
        await this.UpdateSessionEventVisibilityAsync(preferences => preferences.WithEventType(eventType, visible));
    }

    private static string EventVisibilityLabel(string eventType)
    {
        return SessionEventVisibility.IsOperationalEventType(eventType) ? $"{eventType} (operational)" : eventType;
    }

    private static UIElement BuildChatMessageRow(ChatMessage message)
    {
        var role = string.IsNullOrWhiteSpace(message.Role) ? "message" : message.Role.Trim();
        var isUser = string.Equals(role, "user", StringComparison.OrdinalIgnoreCase);
        var wrapper = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = isUser ? XamlHorizontalAlignment.Right : XamlHorizontalAlignment.Left,
            MaxWidth = 680,
        };
        wrapper.Children.Add(new TextBlock
        {
            Text = role.ToUpperInvariant(),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });
        wrapper.Children.Add(new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            Background = isUser
                ? new SolidColorBrush(AccentBrush.Color) { Opacity = 0.18 }
                : ResourceBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Child = new TextBlock
            {
                Text = message.Text,
                TextWrapping = TextWrapping.Wrap,
            },
        });
        return wrapper;
    }

    private string GetActiveChatSessionKey(string? sessionKey = null)
    {
        return string.IsNullOrWhiteSpace(sessionKey)
            ? string.IsNullOrWhiteSpace(this.chatSessionInput.Text)
                ? AppPreferences.Default.ChatSessionKey
                : this.chatSessionInput.Text.Trim()
            : sessionKey.Trim();
    }

    private static UIElement BuildChatEventRow(GatewayRealtimeEvent @event)
    {
        return new TextBlock
        {
            Text = $"event:{@event.Name} {@event.Payload?.ToString() ?? ""}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        };
    }

    private void RenderSessions()
    {
        this.sessionsRefreshButton.IsEnabled = this.appState.Realtime.State == GatewayRealtimeState.Connected;
        this.sessionsList.Children.Clear();
        if (this.latestSessions.Count == 0)
        {
            this.sessionsStatusText.Text = !string.IsNullOrWhiteSpace(this.latestSessionsError)
                ? $"Sessions unavailable: {this.latestSessionsError}"
                : this.appState.Realtime.State == GatewayRealtimeState.Connected
                ? "No sessions returned by the gateway."
                : "Connect to the Gateway to load sessions.";
            this.sessionsList.Children.Add(new TextBlock
            {
                Text = this.sessionsStatusText.Text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
            });
            return;
        }

        var activeSession = string.IsNullOrWhiteSpace(this.chatSessionInput.Text)
            ? AppPreferences.Default.ChatSessionKey
            : this.chatSessionInput.Text.Trim();
        this.sessionsStatusText.Text = $"{this.latestSessions.Count} session{Plural(this.latestSessions.Count)} available. Active chat session: {activeSession}.";
        foreach (var session in this.latestSessions)
        {
            this.sessionsList.Children.Add(this.BuildSessionRow(session, activeSession));
        }
    }

    private UIElement BuildSessionRow(SessionSummary session, string activeSession)
    {
        var body = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        var details = new StackPanel { Spacing = 6 };
        details.Children.Add(new TextBlock
        {
            Text = session.DisplayName,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        details.Children.Add(new TextBlock
        {
            Text = session.Key,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });
        details.Children.Add(BuildDashboardRow("Kind", session.Kind));
        details.Children.Add(BuildDashboardRow("Agent", session.AgentId ?? "unknown"));
        details.Children.Add(BuildDashboardRow("Channel", session.Channel ?? "unknown"));
        details.Children.Add(BuildDashboardRow("State", SessionStateLabel(session)));
        details.Children.Add(BuildDashboardRow("Updated", session.UpdatedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "unknown"));
        body.Children.Add(details);

        var selectButton = new XamlButton
        {
            Content = string.Equals(session.Key, activeSession, StringComparison.Ordinal) ? "Active" : "Use in Chat",
            IsEnabled = !string.Equals(session.Key, activeSession, StringComparison.Ordinal),
            Command = this.CreateCommand(async () => await this.SelectChatSessionAsync(session.Key)),
        };
        AutomationProperties.SetName(selectButton, $"Use {session.Key} in Chat");
        Grid.SetColumn(selectButton, 1);
        body.Children.Add(selectButton);
        return BuildDashboardCard(null, body);
    }

    private async Task SelectChatSessionAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return;
        }

        var normalized = sessionKey.Trim();
        await this.appState.Preferences.UpdateAsync(current => current with { ChatSessionKey = normalized });
        this.chatSessionInput.Text = normalized;
        this.RenderSessions();
        await this.RecordActivityAsync("chat", "Chat session selected", $"Selected chat session {normalized}.", WindowsNavigationDestination.Chat);
        await this.RefreshChatAsync();
        this.ShowDestination(WindowsNavigationDestination.Chat);
    }

    private static string SessionStateLabel(SessionSummary session)
    {
        if (session.HasActiveRun)
        {
            return "active";
        }

        return string.IsNullOrWhiteSpace(session.Status) ? "idle" : session.Status!;
    }

    private async Task RefreshApprovalsAsync()
    {
        this.latestApprovals = await this.appState.Realtime.ListApprovalsAsync();
        if (await this.ApplyApprovalPolicyAsync())
        {
            this.latestApprovals = await this.appState.Realtime.ListApprovalsAsync();
        }
        this.approvalsLoaded = true;
        if (this.notificationPreferences.ApprovalAlerts &&
            this.latestApprovals.Count > 0 &&
            this.latestApprovals.Count != this.lastNotifiedApprovalCount)
        {
            this.ShowNotification(
                WindowsNavigationDestination.Approvals,
                "OpenClaw approval",
                $"{this.latestApprovals.Count} approval request{Plural(this.latestApprovals.Count)} pending.",
                WindowsNotificationKind.Approval);
        }
        this.lastNotifiedApprovalCount = this.latestApprovals.Count;
        this.RenderApprovals();
        this.RenderHomeDashboard();
    }

    private void RenderApprovals()
    {
        var summary = OperatorWorkflowSummary.Create(
            this.latestApprovals,
            this.latestPairingRequests,
            this.coordinator.RealtimeState);
        this.approvalsStatusText.Text = this.approvalsLoaded ? summary.ApprovalsStatus : "Approvals not checked yet";
        this.approvalsList.Children.Clear();
        if (!this.approvalsLoaded)
        {
            this.approvalsList.Children.Add(BuildEmptyWorkflowCard(
                "Approvals not checked yet",
                "Refresh approvals after connecting to the Gateway."));
            return;
        }
        if (this.latestApprovals.Count == 0)
        {
            this.approvalsList.Children.Add(BuildEmptyWorkflowCard(
                "No approvals pending",
                "Command approval requests will appear here when OpenClaw needs operator confirmation."));
            return;
        }

        foreach (var approval in this.latestApprovals)
        {
            this.approvalsList.Children.Add(this.BuildApprovalCard(approval));
        }
    }

    private UIElement BuildApprovalCard(PendingApproval approval)
    {
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(approval.Command) ? "Command approval requested" : approval.Command,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(BuildWorkflowMetadata("Approval", approval.Id));
        if (!string.IsNullOrWhiteSpace(approval.Cwd))
        {
            body.Children.Add(BuildWorkflowMetadata("Working directory", approval.Cwd));
        }
        if (!string.IsNullOrWhiteSpace(approval.AgentId))
        {
            body.Children.Add(BuildWorkflowMetadata("Agent", approval.AgentId));
        }
        if (!string.IsNullOrWhiteSpace(approval.SessionKey))
        {
            body.Children.Add(BuildWorkflowMetadata("Session", approval.SessionKey));
        }
        body.Children.Add(BuildWorkflowMetadata(
            "Risk",
            WindowsApprovalPolicyEvaluator.IsRisky(approval.Command) ? "risky" : "safe"));

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        var allowButton = new XamlButton
        {
            Content = "Allow once",
            AccessKey = "A",
            Command = this.CreateCommand(async () => await this.ResolveApprovalWithHistoryAsync(
                approval,
                "allow-once",
                "Approval allowed once")),
        };
        AutomationProperties.SetName(allowButton, $"Allow approval {approval.Id} once");
        buttons.Children.Add(allowButton);
        if (this.policyPreferences.ApprovalPolicy == WindowsApprovalPolicyPreference.AskEveryTime)
        {
            var rememberButton = new XamlButton
            {
                Content = "Allow && remember",
                Command = this.CreateCommand(async () => await this.RememberApprovalAndAllowAsync(approval)),
            };
            AutomationProperties.SetName(rememberButton, $"Allow approval {approval.Id} and remember it");
            buttons.Children.Add(rememberButton);
        }
        var denyButton = new XamlButton
        {
            Content = "Deny",
            AccessKey = "D",
            Command = this.CreateCommand(async () => await this.ResolveApprovalWithHistoryAsync(
                approval,
                "deny",
                "Approval denied")),
        };
        AutomationProperties.SetName(denyButton, $"Deny approval {approval.Id}");
        buttons.Children.Add(denyButton);
        body.Children.Add(buttons);
        return BuildDashboardCard(null, body);
    }

    private async Task<bool> ApplyApprovalPolicyAsync()
    {
        var handledAny = false;
        foreach (var approval in this.latestApprovals.ToArray())
        {
            if (WindowsApprovalPolicyEvaluator.ShouldAutoAllow(this.policyPreferences, approval.Command))
            {
                await this.ResolveApprovalWithHistoryAsync(
                    approval,
                    "allow-once",
                    "Approval auto-allowed",
                    refreshAfter: false);
                handledAny = true;
                continue;
            }

            if (WindowsApprovalPolicyEvaluator.ShouldAutoDeny(this.policyPreferences, approval.Command))
            {
                await this.ResolveApprovalWithHistoryAsync(
                    approval,
                    "deny",
                    "Approval auto-denied",
                    refreshAfter: false);
                handledAny = true;
            }
        }

        return handledAny;
    }

    private async Task RememberApprovalAndAllowAsync(PendingApproval approval)
    {
        if (string.IsNullOrWhiteSpace(approval.Command))
        {
            await this.ResolveApprovalWithHistoryAsync(approval, "allow-once", "Approval allowed once");
            return;
        }

        var updated = await this.appState.Preferences.UpdateAsync(current => current with
        {
            Policy = current.Policy with
            {
                RememberedAllowedCommands = current.Policy.RememberedAllowedCommands
                    .Append(approval.Command.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
            },
        });
        this.policyPreferences = updated.Policy;
        await this.ResolveApprovalWithHistoryAsync(approval, "allow-once", "Approval allowed and remembered");
    }

    private async Task ResolveApprovalWithHistoryAsync(
        PendingApproval approval,
        string decision,
        string title,
        bool refreshAfter = true)
    {
        await this.appState.Realtime.ResolveApprovalAsync(approval.Id, decision);
        await this.RecordActivityAsync(
            "approval",
            title,
            string.IsNullOrWhiteSpace(approval.Command) ? approval.Id : approval.Command,
            WindowsNavigationDestination.Approvals,
            approval.Command);
        if (refreshAfter)
        {
            await this.RefreshApprovalsAsync();
        }
    }

    private async Task RefreshPairingAsync()
    {
        try
        {
            this.latestPairingRequests = await this.appState.Realtime.ListPairingRequestsAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("WebSocket is not connected", StringComparison.OrdinalIgnoreCase))
        {
            this.latestPairingRequests = [];
            this.pairingLoaded = true;
            this.RenderPairing();
            this.RenderHomeDashboard();
            return;
        }
        this.pairingLoaded = true;
        if (this.notificationPreferences.PairingAlerts &&
            this.latestPairingRequests.Count > 0 &&
            this.latestPairingRequests.Count != this.lastNotifiedPairingCount)
        {
            this.ShowNotification(
                WindowsNavigationDestination.Pairing,
                "OpenClaw pairing",
                $"{this.latestPairingRequests.Count} pairing request{Plural(this.latestPairingRequests.Count)} pending.",
                WindowsNotificationKind.Pairing);
        }
        this.lastNotifiedPairingCount = this.latestPairingRequests.Count;
        this.RenderPairing();
        this.RenderHomeDashboard();
    }

    private void RenderPairing()
    {
        var summary = OperatorWorkflowSummary.Create(
            this.latestApprovals,
            this.latestPairingRequests,
            this.coordinator.RealtimeState);
        this.pairingStatusText.Text = this.pairingLoaded ? summary.PairingStatus : "Pairing not checked yet";
        this.pairingList.Children.Clear();
        if (!this.pairingLoaded)
        {
            this.pairingList.Children.Add(BuildEmptyWorkflowCard(
                "Pairing not checked yet",
                summary.PairingReadiness));
            return;
        }
        if (this.latestPairingRequests.Count == 0)
        {
            this.pairingList.Children.Add(BuildEmptyWorkflowCard(
                "No pairing requests pending",
                summary.PairingReadiness));
            return;
        }

        foreach (var request in this.latestPairingRequests)
        {
            this.pairingList.Children.Add(this.BuildPairingCard(request));
        }
    }

    private UIElement BuildPairingCard(PairingRequest request)
    {
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(request.DisplayName) ? request.DeviceId : request.DisplayName,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(BuildWorkflowMetadata("Kind", request.Kind));
        body.Children.Add(BuildWorkflowMetadata("Device", request.DeviceId));
        body.Children.Add(BuildWorkflowMetadata("Request", request.RequestId));

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        var approveButton = new XamlButton
        {
            Content = "Approve",
            AccessKey = "A",
            Command = this.CreateCommand(async () =>
            {
                await this.appState.Realtime.ResolvePairingAsync(request, approve: true);
                await this.RefreshPairingAsync();
            }),
        };
        AutomationProperties.SetName(approveButton, $"Approve {request.Kind} pairing request {request.RequestId}");
        buttons.Children.Add(approveButton);
        var rejectButton = new XamlButton
        {
            Content = "Reject",
            AccessKey = "J",
            Command = this.CreateCommand(async () =>
            {
                await this.appState.Realtime.ResolvePairingAsync(request, approve: false);
                await this.RefreshPairingAsync();
            }),
        };
        AutomationProperties.SetName(rejectButton, $"Reject {request.Kind} pairing request {request.RequestId}");
        buttons.Children.Add(rejectButton);
        body.Children.Add(buttons);
        return BuildDashboardCard(null, body);
    }

    private static UIElement BuildEmptyWorkflowCard(string title, string detail)
    {
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        body.Children.Add(new TextBlock
        {
            Text = detail,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });
        return BuildDashboardCard(null, body);
    }

    private static UIElement BuildWorkflowMetadata(string label, string? value)
    {
        return BuildDashboardRow(label, string.IsNullOrWhiteSpace(value) ? "unknown" : value);
    }

    private void RenderSettingsSummary(AppPreferences preferences)
    {
        this.settingsText.Text =
            $"Open main window on launch: {preferences.OpenMainWindowOnLaunch}\n" +
            $"Theme: {preferences.ThemePreference}\n" +
            $"Accent color: {preferences.AccentColorPreference}{FormatOptionalColor(preferences.CustomAccentColor)}\n" +
            $"Color theme: {preferences.ColorThemePreference}{FormatOptionalColor(preferences.CustomColorTheme)}\n" +
            $"Last status: {preferences.LastStatus ?? "unknown"}\n" +
            $"Last checked: {preferences.LastStatusCheckedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "never"}\n" +
            $"Device token cached: {!string.IsNullOrWhiteSpace(preferences.DeviceToken)}\n" +
            $"Canvas node: {preferences.CanvasNodeEnabled}\n" +
            $"Voice controls: {preferences.VoiceControlsEnabled}\n" +
            $"Global hotkey: {preferences.GlobalHotkeyEnabled}\n" +
            $"Approval policy: {preferences.Policy.ApprovalPolicy}\n" +
            $"Unsafe URLs blocked: {preferences.Policy.BlockUnsafeUrls}\n" +
            $"Redaction enabled: {preferences.Policy.RedactSensitiveContent}\n" +
            $"Remembered approvals: {preferences.Policy.RememberedAllowedCommands.Count}\n" +
            $"Tunnel auto-start: {preferences.Topology.AutoStartTunnel}\n" +
            $"Tunnel host: {preferences.Topology.SshHost}\n" +
            $"Tunnel route: localhost:{preferences.Topology.LocalPort} -> {preferences.Topology.RemoteHost}:{preferences.Topology.RemotePort}\n" +
            $"Structured diagnostics: {preferences.Diagnostics.StructuredDiagnosticsEnabled}\n" +
            $"History retention: {preferences.Diagnostics.ActivityRetentionCount}\n" +
            $"Notification retention: {preferences.NotificationRules.HistoryRetentionCount}\n" +
            $"Approval alerts: {preferences.NotificationPreferences.ApprovalAlerts}\n" +
            $"Pairing alerts: {preferences.NotificationPreferences.PairingAlerts}\n" +
            $"Gateway health alerts: {preferences.NotificationPreferences.GatewayHealthAlerts}\n" +
            $"Device permission alerts: {preferences.NotificationPreferences.DevicePermissionAlerts}\n" +
            $"Notification rules: {preferences.NotificationRules.Rules.Count}";
    }

    private static string FormatOptionalColor(Color? color)
    {
        return color is { } value
            ? $" #{value.R:X2}{value.G:X2}{value.B:X2}"
            : "";
    }

    private void RenderSettingsStorage()
    {
        this.settingsStorageRows.Children.Clear();
        this.settingsStorageRows.Children.Add(BuildDashboardRow("Preferences", this.appState.Preferences.Path));
        this.settingsStorageRows.Children.Add(BuildDashboardRow("App crash log", CrashLog.Path));
        this.settingsStorageRows.Children.Add(BuildDashboardRow("Gateway log", this.coordinator.LogPath ?? "unknown"));
        this.settingsStorageRows.Children.Add(BuildDashboardRow("Activity history", this.appState.ActivityHistory.Path));
        this.settingsStorageRows.Children.Add(BuildDashboardRow("Notification history", this.appState.NotificationHistory.Path));
        this.settingsStorageRows.Children.Add(BuildDashboardRow(
            "Structured diagnostics",
            this.appState.Diagnostics.ResolvePath(this.diagnosticsPreferences.StructuredDiagnosticsPath)));
        this.settingsStorageRows.Children.Add(BuildDashboardRow("Captures", this.appState.DeviceCapabilities.CaptureRoot));
        this.settingsStorageRows.Children.Add(BuildDashboardRow("Speech clips", this.appState.TextToSpeech.OutputRoot));
        if (!string.IsNullOrWhiteSpace(this.latestSupportSummaryArtifactPath))
        {
            this.settingsStorageRows.Children.Add(BuildDashboardRow("Latest support artifact", this.latestSupportSummaryArtifactPath));
        }
        this.settingsStorageRows.Children.Add(BuildReservedSettingsRow(
            "Minimize to tray",
            "Reserved",
            "Future app-local tray window behavior."));
        this.settingsStorageRows.Children.Add(BuildReservedSettingsRow(
            "Tray quick actions",
            "Reserved",
            "Future app-local tray action selection."));
    }

    private void RenderTopologySnapshot(AppPreferences preferences)
    {
        var snapshot = this.appState.Topology.CreateSnapshot(
            preferences,
            this.coordinator.GatewayStatus,
            this.appState.CanvasNode.A2UIHostUrl,
            this.appState.Tunnel.Status);
        this.topologySummaryText.Text = snapshot.TunnelSummary;
        this.tunnelStatusText.Text = this.appState.Tunnel.Status.LastError is { Length: > 0 } error
            ? $"Tunnel: {this.appState.Tunnel.Status.Summary}\n{error}"
            : $"Tunnel: {this.appState.Tunnel.Status.Summary}";
        this.topologyRows.Children.Clear();
        foreach (var diagnostic in snapshot.Diagnostics)
        {
            this.topologyRows.Children.Add(BuildDashboardRow(
                diagnostic.Label,
                $"{diagnostic.Endpoint} ({diagnostic.State})"));
            this.topologyRows.Children.Add(new TextBlock
            {
                Text = diagnostic.Detail,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
            });
        }
    }

    private void RenderActivityHistory()
    {
        this.logsActivityRows.Children.Clear();
        var entries = this.appState.ActivityHistory.Entries.Take(8).ToArray();
        if (entries.Length == 0)
        {
            this.logsActivityRows.Children.Add(BuildDashboardRow("Latest", "No activity recorded yet."));
            return;
        }

        this.homeActivityText.Text = string.Join(
            Environment.NewLine,
            entries.Take(3).Select(entry =>
                $"{entry.CreatedAt.ToLocalTime():g} {entry.Title}: {entry.Detail}"));
        foreach (var entry in entries)
        {
            this.logsActivityRows.Children.Add(BuildDashboardCard(
                null,
                BuildSettingsSection(
                    BuildTimestampSummaryRow(entry.CreatedAt, $"{entry.Category}: {entry.Title}"),
                    BuildDashboardRow("Destination", entry.Destination ?? "none"),
                    new TextBlock
                    {
                        Text = entry.Detail,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
                    })));
        }
    }

    private void RenderNotificationHistory()
    {
        this.logsNotificationRows.Children.Clear();
        var entries = this.appState.NotificationHistory.Entries.Take(8).ToArray();
        if (entries.Length == 0)
        {
            this.logsNotificationRows.Children.Add(BuildDashboardRow(
                this.S("Shell.Logs.Notifications.EmptyLabel", "Latest"),
                this.S("Shell.Logs.Notifications.EmptyValue", "No notification history saved yet.")));
            return;
        }

        foreach (var entry in entries)
        {
            this.logsNotificationRows.Children.Add(BuildDashboardCard(
                null,
                BuildSettingsSection(
                    BuildTimestampSummaryRow(entry.CreatedAt, $"{entry.Kind}: {entry.Title}"),
                    BuildDashboardRow("Category", entry.Category),
                    BuildDashboardRow("Destination", entry.Destination),
                    new TextBlock
                    {
                        Text = entry.Message,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
                    })));
        }
    }

    private void RenderSupportSummarySnapshot()
    {
        var summary = this.BuildOperationalSupportSummary(this.LoadCurrentShellPreferences());
        this.supportSummaryText.Text =
            $"{summary.GeneratedAt.ToLocalTime():g}\n" +
            $"{summary.GatewayUrl}\n" +
            this.SF(
                "Shell.Logs.Support.SnapshotCounts",
                "{0} activity item(s), {1} notification item(s), {2} rule(s).",
                summary.RecentActivity.Count,
                summary.RecentNotifications.Count,
                summary.NotificationRules.Count);
    }

    private WindowsOperationalSupportSummary BuildOperationalSupportSummary(AppPreferences preferences)
    {
        return this.appState.OperationalSupport.Build(
            preferences,
            this.appState.Diagnostics,
            this.appState.ActivityHistory,
            this.appState.NotificationHistory);
    }

    private async Task CopyActivityHistoryAsync()
    {
        var text = this.appState.ActivityHistory.Entries.Count == 0
            ? this.S("Shell.Logs.Activity.EmptyValue", "No activity recorded yet.")
            : string.Join(
                Environment.NewLine,
                this.appState.ActivityHistory.Entries.Select(entry =>
                    $"[{entry.CreatedAt:O}] {entry.Category}: {entry.Title} ({entry.Destination ?? "none"}) - {entry.Detail}"));
        CopyTextToClipboard(text);
        this.rawLogsText.Text = text;
        await this.RecordActivityAsync("logs", "Activity history copied", "Copied activity history to the clipboard.", WindowsNavigationDestination.Logs);
    }

    private async Task ClearActivityHistoryAsync()
    {
        await this.appState.ActivityHistory.ClearAsync();
        this.homeActivityText.Text = this.S("Shell.Logs.Activity.EmptyValue", "No activity recorded yet.");
        this.RenderActivityHistory();
        this.RenderLogsDiagnostics();
    }

    private async Task CopyNotificationHistoryAsync()
    {
        var text = this.appState.NotificationHistory.Entries.Count == 0
            ? this.S("Shell.Logs.Notifications.EmptyValue", "No notification history saved yet.")
            : string.Join(
                Environment.NewLine,
                this.appState.NotificationHistory.Entries.Select(entry =>
                    $"[{entry.CreatedAt:O}] {entry.Kind}/{entry.Category}: {entry.Title} ({entry.Destination}) - {entry.Message}"));
        CopyTextToClipboard(text);
        this.rawLogsText.Text = text;
        await this.RecordActivityAsync("logs", "Notification history copied", "Copied notification history to the clipboard.", WindowsNavigationDestination.Logs);
    }

    private async Task ClearNotificationHistoryAsync()
    {
        this.appState.Notifications.Clear();
        await this.appState.NotificationHistory.ClearAsync();
        this.RenderNotificationActivity();
        this.RenderNotificationHistory();
        this.RenderLogsDiagnostics();
    }

    private async Task CopySupportSummaryAsync()
    {
        var summary = this.BuildOperationalSupportSummary(this.LoadCurrentShellPreferences());
        var text = summary.ToPlainText();
        CopyTextToClipboard(text);
        this.rawLogsText.Text = text;
        this.supportSummaryText.Text = text;
        await this.RecordActivityAsync("logs", "Support summary copied", "Copied the operational support summary.", WindowsNavigationDestination.Logs);
    }

    private async Task SaveSupportSummaryArtifactAsync()
    {
        var summary = this.BuildOperationalSupportSummary(this.LoadCurrentShellPreferences());
        var directory = Path.GetDirectoryName(this.appState.NotificationHistory.Path) ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var artifactPath = Path.Combine(directory, $"support-summary-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.json");
        await this.appState.OperationalSupport.WriteArtifactAsync(artifactPath, summary);
        this.latestSupportSummaryArtifactPath = artifactPath;
        this.supportSummaryText.Text = this.SF(
            "Shell.Logs.Support.ArtifactSaved",
            "Saved support artifact to {0}.",
            artifactPath);
        this.rawLogsText.Text = summary.ToPlainText();
        this.RenderSettingsStorage();
        WindowsShell.OpenFileInExplorer(artifactPath);
        await this.RecordActivityAsync("logs", "Support artifact saved", this.supportSummaryText.Text, WindowsNavigationDestination.Logs, artifactPath);
    }

    private async Task RecordActivityAsync(
        string category,
        string title,
        string detail,
        string? destination = null,
        string? raw = null)
    {
        try
        {
            var preferences = await this.appState.Preferences.LoadAsync();
            var storedDetail = preferences.Policy.RedactSensitiveContent
                ? this.appState.SecretRedactor.Redact(detail)
                : detail;
            var storedRaw = preferences.Policy.RedactSensitiveContent
                ? this.appState.SecretRedactor.Redact(raw)
                : raw;
            await this.appState.ActivityHistory.AddAsync(
                category,
                title,
                storedDetail,
                destination,
                preferences.Diagnostics.ActivityRetentionCount);
            if (preferences.Diagnostics.StructuredDiagnosticsEnabled)
            {
                await this.appState.Diagnostics.WriteAsync(
                    this.appState.Diagnostics.ResolvePath(preferences.Diagnostics.StructuredDiagnosticsPath),
                    new WindowsDiagnosticEntry(
                        DateTimeOffset.Now,
                        category,
                        title,
                        storedDetail,
                        destination,
                        storedRaw));
            }

            this.RenderActivityHistory();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
        }
    }

    private async Task SaveSettingsAsync()
    {
        var approvalPolicy = this.approvalPolicyInput.SelectedItem is XamlComboBoxItem
            {
                Tag: WindowsApprovalPolicyPreference selectedPolicy,
            }
            ? selectedPolicy
            : WindowsPolicyPreferences.Default.ApprovalPolicy;
        this.policyPreferences = this.policyPreferences with { ApprovalPolicy = approvalPolicy };
        this.notificationRulePreferences = this.CollectNotificationRulePreferences();
        this.diagnosticsPreferences = this.diagnosticsPreferences with
        {
            StructuredDiagnosticsPath = this.diagnosticsPathInput.Text.Trim(),
            ActivityRetentionCount = ParsePositiveIntOrDefault(
                this.activityRetentionCountInput.Text,
                this.diagnosticsPreferences.ActivityRetentionCount,
                WindowsDiagnosticsPreferences.Default.ActivityRetentionCount),
        };
        this.topologyPreferences = this.topologyPreferences with
        {
            SshHost = this.tunnelHostInput.Text.Trim(),
            RemoteHost = string.IsNullOrWhiteSpace(this.tunnelRemoteHostInput.Text)
                ? WindowsTopologyPreferences.Default.RemoteHost
                : this.tunnelRemoteHostInput.Text.Trim(),
            LocalPort = ParsePositiveIntOrDefault(
                this.tunnelLocalPortInput.Text,
                this.topologyPreferences.LocalPort,
                WindowsTopologyPreferences.Default.LocalPort),
            RemotePort = ParsePositiveIntOrDefault(
                this.tunnelRemotePortInput.Text,
                this.topologyPreferences.RemotePort,
                WindowsTopologyPreferences.Default.RemotePort),
        };
        // Persist secrets through AppPreferencesStore so tokens stay in the credential store.
        await this.appState.Preferences.UpdateAsync(current => current with
        {
            OpenMainWindowOnLaunch = this.openMainWindowOnLaunch,
            GatewayUrl = string.IsNullOrWhiteSpace(this.gatewayUrlInput.Text)
                ? AppPreferences.Default.GatewayUrl
                : this.gatewayUrlInput.Text.Trim(),
            GatewayToken = string.IsNullOrWhiteSpace(this.gatewayTokenInput.Password) ? null : this.gatewayTokenInput.Password.Trim(),
            ChatSessionKey = string.IsNullOrWhiteSpace(this.chatSessionInput.Text)
                ? AppPreferences.Default.ChatSessionKey
                : this.chatSessionInput.Text.Trim(),
            ThemePreference = this.themePreference,
            AccentColorPreference = this.accentColorPreference,
            CustomAccentColor = this.customAccentColor,
            ColorThemePreference = this.colorThemePreference,
            CustomColorTheme = this.customColorTheme,
            CanvasNodeEnabled = this.canvasNodeEnabled,
            SessionEventVisibility = this.sessionEventVisibility.WithObservedEvents(this.chatRealtimeEvents),
            NotificationPreferences = this.notificationPreferences,
            NotificationRules = this.notificationRulePreferences,
            VoiceControlsEnabled = this.voiceControlsEnabled,
            GlobalHotkeyEnabled = this.globalHotkeyEnabled,
            Topology = this.topologyPreferences,
            Diagnostics = this.diagnosticsPreferences,
            Policy = this.policyPreferences,
        });
        await this.RecordActivityAsync("settings", "Settings saved", "Updated Windows companion settings.");
        await this.RefreshAllAsync();
    }

    public async Task HandleActivationAsync(WindowsActivationRequest request)
    {
        this.ShowShell();
        if (!string.IsNullOrWhiteSpace(request.ChatSessionKey))
        {
            await this.SelectChatSessionAsync(request.ChatSessionKey);
        }
        else
        {
            this.ShowDestination(request.Destination);
        }

        if (!string.IsNullOrWhiteSpace(request.SourceUri))
        {
            this.detailText.Text = request.SourceUri;
        }

        await this.RecordActivityAsync(
            "activation",
            "Activation routed",
            $"Opened {request.Destination} from an activation request.",
            request.Destination,
            request.SourceUri);
    }

    private async Task RunTunnelFromSettingsAsync()
    {
        this.topologyPreferences = this.topologyPreferences with
        {
            SshHost = this.tunnelHostInput.Text.Trim(),
            RemoteHost = string.IsNullOrWhiteSpace(this.tunnelRemoteHostInput.Text)
                ? WindowsTopologyPreferences.Default.RemoteHost
                : this.tunnelRemoteHostInput.Text.Trim(),
            LocalPort = ParsePositiveIntOrDefault(
                this.tunnelLocalPortInput.Text,
                this.topologyPreferences.LocalPort,
                WindowsTopologyPreferences.Default.LocalPort),
            RemotePort = ParsePositiveIntOrDefault(
                this.tunnelRemotePortInput.Text,
                this.topologyPreferences.RemotePort,
                WindowsTopologyPreferences.Default.RemotePort),
        };
        await this.appState.Tunnel.StartAsync(this.topologyPreferences);
        await this.RefreshTopologyAsync();
    }

    private async Task RefreshTopologyAsync()
    {
        var preferences = await this.appState.Preferences.LoadAsync();
        this.RenderTopologySnapshot(preferences with { Topology = this.topologyPreferences });
    }

    private static int ParsePositiveIntOrDefault(string? text, int currentValue, int fallbackValue)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
        {
            return value;
        }

        return currentValue > 0 ? currentValue : fallbackValue;
    }

    private async Task RefreshDeviceCapabilitiesAsync()
    {
        var preferences = await this.appState.Preferences.LoadAsync();
        this.notificationPreferences = preferences.NotificationPreferences;
        this.notificationRulePreferences = WindowsNotificationRuleEvaluator.NormalizePreferences(preferences.NotificationRules);
        this.canvasNodeEnabled = preferences.CanvasNodeEnabled;
        this.voiceControlsEnabled = preferences.VoiceControlsEnabled;
        this.globalHotkeyEnabled = preferences.GlobalHotkeyEnabled;
        this.approvalAlertsInput.IsChecked = preferences.NotificationPreferences.ApprovalAlerts;
        this.pairingAlertsInput.IsChecked = preferences.NotificationPreferences.PairingAlerts;
        this.gatewayHealthAlertsInput.IsChecked = preferences.NotificationPreferences.GatewayHealthAlerts;
        this.devicePermissionAlertsInput.IsChecked = preferences.NotificationPreferences.DevicePermissionAlerts;
        this.canvasNodeEnabledInput.IsChecked = preferences.CanvasNodeEnabled;
        this.settingsVoiceControlsInput.IsChecked = preferences.VoiceControlsEnabled;
        this.settingsGlobalHotkeyInput.IsChecked = preferences.GlobalHotkeyEnabled;
        this.SyncHotkeyRegistration(preferences.GlobalHotkeyEnabled);
        this.PopulateTextToSpeechVoices();
        this.browserProxyActionResult = this.appState.BrowserProxy.CreateStatus(preferences, this.coordinator.GatewayStatus).Detail;
        this.latestDevicePermissionStatuses = WindowsDeviceCapabilityService.GetPermissionStatus();
        var unavailableCapabilities = this.latestDevicePermissionStatuses
            .Where(status => string.Equals(status.State, "Unavailable", StringComparison.OrdinalIgnoreCase))
            .Select(status => status.Capability)
            .ToArray();
        var deviceFailureSignature = string.Join("|", unavailableCapabilities);
        if (this.notificationPreferences.DevicePermissionAlerts &&
            unavailableCapabilities.Length > 0 &&
            !string.Equals(this.lastNotifiedDevicePermissionFailures, deviceFailureSignature, StringComparison.Ordinal))
        {
            this.ShowNotification(
                WindowsNavigationDestination.Devices,
                "OpenClaw device permissions",
                $"Unavailable: {string.Join(", ", unavailableCapabilities)}.",
                WindowsNotificationKind.DevicePermission);
        }
        this.lastNotifiedDevicePermissionFailures = deviceFailureSignature;

        this.mediaDevicesList.Children.Clear();
        try
        {
            var cameras = new List<WindowsMediaDevice>();
            var microphones = new List<WindowsMediaDevice>();
            foreach (var camera in await WindowsDeviceCapabilityService.ListCameraDevicesAsync())
            {
                cameras.Add(camera);
            }
            foreach (var microphone in await WindowsDeviceCapabilityService.ListMicrophoneDevicesAsync())
            {
                microphones.Add(microphone);
            }
            this.mediaDeviceSummary =
                $"Cameras: {cameras.Count}; microphones: {microphones.Count}.";
            foreach (var camera in cameras)
            {
                this.mediaDevicesList.Children.Add(BuildDashboardRow("Camera", DeviceLabel(camera)));
            }
            foreach (var microphone in microphones)
            {
                this.mediaDevicesList.Children.Add(BuildDashboardRow("Microphone", DeviceLabel(microphone)));
            }
        }
        catch (Exception ex)
        {
            this.mediaDeviceSummary = $"Media device enumeration failed: {ex.Message}";
            this.mediaDevicesList.Children.Add(new TextBlock
            {
                Text = this.mediaDeviceSummary,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ResourceBrush("SystemFillColorCriticalBrush"),
            });
        }
        this.RenderDeviceCapabilityCards();
    }

    private void RenderDeviceCapabilityCards()
    {
        this.deviceCapabilityCards.Children.Clear();
        this.deviceCapabilityCards.Children.Add(BuildDashboardCard("Canvas", BuildSettingsSection(
            BuildDashboardRow("Node", this.appState.CanvasNode.State.ToString()),
            BuildDashboardRow("A2UI", this.appState.CanvasNode.A2UIHostUrl ?? "unknown"),
            BuildDashboardRow("Enabled", this.canvasNodeEnabled.ToString()))));
        this.deviceCapabilityCards.Children.Add(this.BuildDeviceCapabilityCard(
            DeviceCapabilityPresentation.Create("Screen", this.latestDevicePermissionStatuses, this.screenActionResult),
            [
                this.DeviceActionButton("Screen", "Capture primary screen", async () => await this.CaptureScreenAsync()),
                this.DeviceActionButton(
                    this.S("Shell.Devices.ScreenRecording.StartButton", "Record"),
                    this.S("Shell.Devices.ScreenRecording.StartAutomationName", "Start bounded screen recording"),
                    async () => await this.CaptureScreenRecordingAsync()),
            ]));
        this.deviceCapabilityCards.Children.Add(this.BuildScreenRecordingCard());
        this.deviceCapabilityCards.Children.Add(this.BuildDeviceCapabilityCard(
            DeviceCapabilityPresentation.Create("Camera", this.latestDevicePermissionStatuses, this.cameraActionResult),
            [this.DeviceActionButton("Camera", "Capture camera photo", async () => await this.CaptureCameraPhotoAsync())]));
        this.deviceCapabilityCards.Children.Add(this.BuildDeviceCapabilityCard(
            DeviceCapabilityPresentation.Create("Microphone", this.latestDevicePermissionStatuses, this.microphoneActionResult),
            [
                DeviceToggle("Enable voice controls", this.voiceControlsEnabled, value => this.voiceControlsEnabled = value),
                this.DeviceActionButton("Save voice", "Save voice controls preference", async () => await this.SaveDevicePreferencesAsync()),
            ]));
        this.deviceCapabilityCards.Children.Add(this.BuildDeviceCapabilityCard(
            DeviceCapabilityPresentation.Create("Hotkeys", this.latestDevicePermissionStatuses, this.hotkeyActionResult),
            [
                DeviceToggle("Register Ctrl+Shift+Space push-to-talk hotkey", this.globalHotkeyEnabled, value => this.globalHotkeyEnabled = value),
                this.DeviceActionButton("Save hotkey", "Save global hotkey preference", async () => await this.SaveDevicePreferencesAsync()),
            ]));
        this.deviceCapabilityCards.Children.Add(this.BuildDeviceCapabilityCard(
            DeviceCapabilityPresentation.Create("Notifications", this.latestDevicePermissionStatuses, this.notificationActionResult),
            [this.DeviceActionButton("Notify", "Send test notification", () =>
            {
                this.ShowNotification(
                    WindowsNavigationDestination.Devices,
                    "OpenClaw",
                    "Windows companion notifications are available.");
                return Task.CompletedTask;
            })]));
        this.deviceCapabilityCards.Children.Add(this.BuildBrowserProxyCapabilityCard());
        this.deviceCapabilityCards.Children.Add(this.BuildSystemSpeechCapabilityCard());
        this.deviceCapabilityCards.Children.Add(this.BuildDeviceCapabilityCard(
            DeviceCapabilityPresentation.Create("Overlays", this.latestDevicePermissionStatuses, this.overlayActionResult),
            [this.DeviceActionButton("Overlay", "Show test overlay", () =>
            {
                this.ShowOverlay("OpenClaw overlay", "Native Windows overlays are available.");
                return Task.CompletedTask;
            })]));
    }

    private UIElement BuildScreenRecordingCard()
    {
        var presentation = DeviceCapabilityPresentation.Create("Screen recording", this.latestDevicePermissionStatuses, this.screenActionResult);
        this.RecreateScreenRecordingControls();
        this.UpdateScreenRecordingPlanPreview();
        var actions = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        actions.Children.Add(this.cancelScreenRecordingButton);
        actions.Children.Add(this.DeviceActionButton(
            this.S("Shell.Devices.ScreenRecording.OpenFolderButton", "Open captures"),
            this.S("Shell.Devices.ScreenRecording.OpenFolderAutomationName", "Open capture output folder"),
            () =>
            {
                WindowsShell.OpenFileInExplorer(this.appState.DeviceCapabilities.CaptureRoot);
                return Task.CompletedTask;
            }));

        return BuildDashboardCard(
            null,
            BuildSettingsSection(
                this.BuildDeviceCapabilitySummary(presentation),
                BuildSettingsField(
                    this.S("Shell.Devices.ScreenRecording.DurationLabel", "Duration (seconds)"),
                    this.S(
                        "Shell.Devices.ScreenRecording.DurationDetail",
                        "Values are bounded to short captures so the WinUI shell stays responsive."),
                    this.screenRecordingDurationInput),
                BuildSettingsField(
                    this.S("Shell.Devices.ScreenRecording.FpsLabel", "Frames per second"),
                    this.S(
                        "Shell.Devices.ScreenRecording.FpsDetail",
                        "Higher frame rates are clamped to the Windows companion recording limit."),
                    this.screenRecordingFramesPerSecondInput),
                this.screenRecordingPlanText,
                actions));
    }

    private UIElement BuildBrowserProxyCapabilityCard()
    {
        var preferences = this.LoadCurrentShellPreferences();
        var status = this.appState.BrowserProxy.CreateStatus(preferences, this.coordinator.GatewayStatus);
        var presentation = DeviceCapabilityPresentation.Create("Browser proxy", this.latestDevicePermissionStatuses, this.browserProxyActionResult);
        var actions = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        actions.Children.Add(this.DeviceActionButton(
            this.S("Shell.Devices.BrowserProxy.SettingsButton", "Open settings"),
            this.S("Shell.Devices.BrowserProxy.SettingsAutomationName", "Open browser proxy settings"),
            () =>
            {
                this.ShowDestination(WindowsNavigationDestination.Settings);
                return Task.CompletedTask;
            }));
        actions.Children.Add(this.DeviceActionButton(
            this.S("Shell.Devices.BrowserProxy.DashboardButton", "Open dashboard"),
            this.S("Shell.Devices.BrowserProxy.DashboardAutomationName", "Open gateway dashboard"),
            async () => await this.OpenUriWithPolicyAsync(this.coordinator.GatewayStatus?.DashboardUrl, WindowsNavigationDestination.Home)));

        return BuildDashboardCard(
            null,
            BuildSettingsSection(
                this.BuildDeviceCapabilitySummary(presentation),
                BuildDashboardRow(this.S("Shell.Devices.BrowserProxy.StateLabel", "Readiness"), status.State),
                BuildDashboardRow(this.S("Shell.Devices.BrowserProxy.GatewayLabel", "Gateway origin"), status.GatewayOrigin ?? "unknown"),
                BuildDashboardRow(this.S("Shell.Devices.BrowserProxy.PolicyLabel", "Unsafe URL policy"), preferences.Policy.BlockUnsafeUrls.ToString()),
                new TextBlock
                {
                    Text = status.Detail,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
                },
                new TextBlock
                {
                    Text = status.RepairGuidance,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
                },
                actions));
    }

    private UIElement BuildSystemSpeechCapabilityCard()
    {
        var presentation = DeviceCapabilityPresentation.Create("System speech", this.latestDevicePermissionStatuses, this.textToSpeechActionResult);
        var status = this.appState.TextToSpeech.GetStatus();
        this.RecreateTextToSpeechControls();
        var actions = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        actions.Children.Add(this.DeviceActionButton(
            this.S("Shell.Devices.SystemSpeech.SaveButton", "Save clip"),
            this.S("Shell.Devices.SystemSpeech.SaveAutomationName", "Save a Windows speech clip"),
            async () => await this.SynthesizeSpeechAsync()));
        actions.Children.Add(this.DeviceActionButton(
            this.S("Shell.Devices.SystemSpeech.RevealButton", "Reveal clip"),
            this.S("Shell.Devices.SystemSpeech.RevealAutomationName", "Reveal the latest Windows speech clip"),
            () =>
            {
                if (!string.IsNullOrWhiteSpace(this.latestTextToSpeechPath))
                {
                    WindowsShell.OpenFileInExplorer(this.latestTextToSpeechPath);
                }
                else
                {
                    WindowsShell.OpenFileInExplorer(this.appState.TextToSpeech.OutputRoot);
                }

                return Task.CompletedTask;
            }));

        return BuildDashboardCard(
            null,
            BuildSettingsSection(
                this.BuildDeviceCapabilitySummary(presentation),
                BuildDashboardRow(this.S("Shell.Devices.SystemSpeech.StateLabel", "Provider"), status.State),
                BuildDashboardRow(this.S("Shell.Devices.SystemSpeech.DefaultVoiceLabel", "Default voice"), status.DefaultVoice ?? "unknown"),
                BuildDashboardRow(this.S("Shell.Devices.SystemSpeech.VoiceCountLabel", "Installed voices"), status.InstalledVoiceCount.ToString(CultureInfo.InvariantCulture)),
                BuildSettingsField(
                    this.S("Shell.Devices.SystemSpeech.VoiceLabel", "Voice"),
                    this.S(
                        "Shell.Devices.SystemSpeech.VoiceDetail",
                        "Speech clips are written to local files. Pick a Windows voice or leave the default selected."),
                    this.textToSpeechVoiceInput),
                BuildSettingsField(
                    this.S("Shell.Devices.SystemSpeech.TextLabel", "Speech text"),
                    this.S(
                        "Shell.Devices.SystemSpeech.TextDetail",
                        "Keep the clip short. The Windows companion saves the synthesized result instead of auto-playing it."),
                    this.textToSpeechInput),
                actions));
    }

    private UIElement BuildDeviceCapabilitySummary(DeviceCapabilityPresentation presentation)
    {
        var summary = new StackPanel { Spacing = 10 };
        summary.Children.Add(new TextBlock
        {
            Text = presentation.Capability,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        summary.Children.Add(BuildDashboardRow("Permission", presentation.State));
        summary.Children.Add(BuildDashboardRow("Detail", presentation.Detail));
        summary.Children.Add(BuildDashboardRow("Last action", presentation.LastAction));
        summary.Children.Add(BuildDashboardRow("Repair", presentation.RepairGuidance));
        if (presentation.Capability is "Camera" or "Microphone")
        {
            summary.Children.Add(BuildDashboardRow("Devices", this.mediaDeviceSummary));
        }

        return summary;
    }

    private UIElement BuildDeviceCapabilityCard(
        DeviceCapabilityPresentation presentation,
        IEnumerable<UIElement> actions)
    {
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(this.BuildDeviceCapabilitySummary(presentation));
        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        foreach (var action in actions)
        {
            buttons.Children.Add(action);
        }
        body.Children.Add(buttons);
        return BuildDashboardCard(null, body);
    }

    private XamlButton DeviceActionButton(string label, string automationName, Func<Task> execute)
    {
        var button = new XamlButton
        {
            Content = label,
            Command = this.CreateCommand(execute),
        };
        AutomationProperties.SetName(button, automationName);
        return button;
    }

    private static XamlCheckBox DeviceToggle(string label, bool isChecked, Action<bool> update)
    {
        var toggle = new XamlCheckBox
        {
            Content = label,
            IsChecked = isChecked,
        };
        toggle.Checked += (_, _) => update(true);
        toggle.Unchecked += (_, _) => update(false);
        AutomationProperties.SetName(toggle, label);
        return toggle;
    }

    private static string DeviceLabel(WindowsMediaDevice device)
    {
        var state = device.IsEnabled ? "enabled" : "disabled";
        return $"{device.Name} ({state})";
    }

    private AppPreferences LoadCurrentShellPreferences()
    {
        return AppPreferences.Default with
        {
            GatewayUrl = string.IsNullOrWhiteSpace(this.gatewayUrlInput.Text) ? AppPreferences.Default.GatewayUrl : this.gatewayUrlInput.Text.Trim(),
            ThemePreference = this.themePreference,
            AccentColorPreference = this.accentColorPreference,
            CustomAccentColor = this.customAccentColor,
            ColorThemePreference = this.colorThemePreference,
            CustomColorTheme = this.customColorTheme,
            NotificationPreferences = this.notificationPreferences,
            NotificationRules = this.notificationRulePreferences,
            Topology = this.topologyPreferences,
            Diagnostics = this.diagnosticsPreferences,
            Policy = this.policyPreferences,
            CanvasNodeEnabled = this.canvasNodeEnabled,
            VoiceControlsEnabled = this.voiceControlsEnabled,
            GlobalHotkeyEnabled = this.globalHotkeyEnabled,
        };
    }

    private void RecreateScreenRecordingControls()
    {
        var durationText = this.screenRecordingDurationInput.Text;
        var framesPerSecondText = this.screenRecordingFramesPerSecondInput.Text;

        this.screenRecordingDurationInput.TextChanged -= this.OnScreenRecordingSettingsChanged;
        this.screenRecordingFramesPerSecondInput.TextChanged -= this.OnScreenRecordingSettingsChanged;

        this.screenRecordingDurationInput = new XamlTextBox
        {
            PlaceholderText = WindowsScreenRecordingOptions.Default.Duration.TotalSeconds.ToString(CultureInfo.InvariantCulture),
            Text = durationText,
        };
        this.screenRecordingFramesPerSecondInput = new XamlTextBox
        {
            PlaceholderText = WindowsScreenRecordingOptions.Default.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
            Text = framesPerSecondText,
        };
        this.screenRecordingPlanText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        };
        this.cancelScreenRecordingButton = new XamlButton
        {
            Content = this.S("Shell.Devices.ScreenRecording.CancelButton", "Cancel"),
            Command = this.CreateCommand(() =>
            {
                this.CancelScreenRecording();
                return Task.CompletedTask;
            }),
        };

        AutomationProperties.SetName(this.screenRecordingDurationInput, this.S("Shell.Devices.ScreenRecording.Duration.AutomationName", "Screen recording duration in seconds"));
        AutomationProperties.SetName(this.screenRecordingFramesPerSecondInput, this.S("Shell.Devices.ScreenRecording.Fps.AutomationName", "Screen recording frames per second"));
        AutomationProperties.SetName(this.cancelScreenRecordingButton, this.S("Shell.Devices.ScreenRecording.CancelAutomationName", "Cancel screen recording"));

        this.screenRecordingDurationInput.TextChanged += this.OnScreenRecordingSettingsChanged;
        this.screenRecordingFramesPerSecondInput.TextChanged += this.OnScreenRecordingSettingsChanged;
    }

    private void RecreateTextToSpeechControls()
    {
        var speechText = this.textToSpeechInput.Text;
        var selectedVoiceId = this.textToSpeechVoiceInput.SelectedItem is XamlComboBoxItem { Tag: string currentVoiceId }
            ? currentVoiceId
            : null;

        this.textToSpeechInput = new XamlTextBox
        {
            AcceptsReturn = true,
            MinHeight = 88,
            TextWrapping = TextWrapping.Wrap,
            PlaceholderText = this.S(
                "Shell.Devices.SystemSpeech.Input.Placeholder",
                "Enter a short OpenClaw response to save as a Windows speech clip."),
            Text = speechText,
        };
        this.textToSpeechVoiceInput = new XamlComboBox();

        AutomationProperties.SetName(this.textToSpeechInput, this.S("Shell.Devices.SystemSpeech.Input.AutomationName", "Speech clip text"));
        AutomationProperties.SetName(this.textToSpeechVoiceInput, this.S("Shell.Devices.SystemSpeech.Voice.AutomationName", "Windows speech voice"));

        this.PopulateTextToSpeechVoices(selectedVoiceId);
    }

    private void PopulateTextToSpeechVoices(string? selectedVoiceId = null)
    {
        selectedVoiceId ??= this.textToSpeechVoiceInput.SelectedItem is XamlComboBoxItem { Tag: string currentVoiceId }
            ? currentVoiceId
            : null;
        var voices = this.appState.TextToSpeech.GetAvailableVoices();
        this.textToSpeechVoiceInput.Items.Clear();
        foreach (var voice in voices)
        {
            this.textToSpeechVoiceInput.Items.Add(new XamlComboBoxItem
            {
                Content = $"{voice.DisplayName} ({voice.Language})",
                Tag = voice.Id,
            });
        }

        if (voices.Count == 0)
        {
            return;
        }

        var targetVoiceId = selectedVoiceId ??
            voices.FirstOrDefault(voice => voice.IsDefault)?.Id ??
            voices[0].Id;
        foreach (var item in this.textToSpeechVoiceInput.Items.OfType<XamlComboBoxItem>())
        {
            if (item.Tag is string voiceId &&
                string.Equals(voiceId, targetVoiceId, StringComparison.Ordinal))
            {
                this.textToSpeechVoiceInput.SelectedItem = item;
                return;
            }
        }
    }

    private void UpdateScreenRecordingPlanPreview()
    {
        try
        {
            var plan = this.appState.DeviceCapabilities.CreateScreenRecordingPlan(this.CreateScreenRecordingOptions());
            this.screenRecordingPlanText.Text = plan.Summary;
            this.cancelScreenRecordingButton.IsEnabled = this.screenRecordingCancellation is not null;
        }
        catch (Exception ex)
        {
            this.screenRecordingPlanText.Text = ex.Message;
            this.cancelScreenRecordingButton.IsEnabled = this.screenRecordingCancellation is not null;
        }
    }

    private WindowsScreenRecordingOptions CreateScreenRecordingOptions()
    {
        var durationSeconds = ParsePositiveIntOrDefault(
            this.screenRecordingDurationInput.Text,
            (int)WindowsScreenRecordingOptions.Default.Duration.TotalSeconds,
            (int)WindowsScreenRecordingOptions.Default.Duration.TotalSeconds);
        var framesPerSecond = ParsePositiveIntOrDefault(
            this.screenRecordingFramesPerSecondInput.Text,
            WindowsScreenRecordingOptions.Default.FramesPerSecond,
            WindowsScreenRecordingOptions.Default.FramesPerSecond);
        return new WindowsScreenRecordingOptions(
            Duration: TimeSpan.FromSeconds(durationSeconds),
            FramesPerSecond: framesPerSecond,
            Prefix: "recording");
    }

    private async Task SaveDevicePreferencesAsync()
    {
        await this.appState.Preferences.UpdateAsync(current => current with
        {
            VoiceControlsEnabled = this.voiceControlsEnabled,
            GlobalHotkeyEnabled = this.globalHotkeyEnabled,
        });
        this.microphoneActionResult = this.voiceControlsEnabled
            ? "Voice controls preference saved as enabled."
            : "Voice controls preference saved as disabled.";
        this.hotkeyActionResult = this.globalHotkeyEnabled
            ? "Global hotkey preference saved as enabled."
            : "Global hotkey preference saved as disabled.";
        this.nativeActionsText.Text = "Device preferences saved.";
        await this.RefreshDeviceCapabilitiesAsync();
    }

    private async Task CaptureScreenAsync()
    {
        this.screenActionResult = "Capturing screen...";
        this.nativeActionsText.Text = this.screenActionResult;
        this.RenderDeviceCapabilityCards();
        var result = await Task.Run(this.appState.DeviceCapabilities.CapturePrimaryScreen);
        this.screenActionResult = result.Detail;
        this.nativeActionsText.Text = this.screenActionResult;
        this.RenderDeviceCapabilityCards();
        if (result.Succeeded && !string.IsNullOrWhiteSpace(result.Path))
        {
            WindowsShell.OpenFileInExplorer(result.Path);
        }
    }

    private async Task CaptureScreenRecordingAsync()
    {
        this.screenRecordingCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        this.screenRecordingCancellation = cancellation;
        this.screenActionResult = this.S("Shell.Devices.ScreenRecording.Started", "Recording screen capture sequence...");
        this.nativeActionsText.Text = this.screenActionResult;
        this.UpdateScreenRecordingPlanPreview();
        this.RenderDeviceCapabilityCards();
        try
        {
            var options = this.CreateScreenRecordingOptions();
            var result = await Task.Run(
                () => this.appState.DeviceCapabilities.CaptureScreenRecordingAsync(options, cancellation.Token),
                cancellation.Token);
            this.screenActionResult = result.Detail;
            this.nativeActionsText.Text = this.screenActionResult;
            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.DirectoryPath))
            {
                WindowsShell.OpenFileInExplorer(result.DirectoryPath);
            }
        }
        catch (OperationCanceledException)
        {
            this.screenActionResult = this.S("Shell.Devices.ScreenRecording.Cancelled", "Screen recording cancelled.");
            this.nativeActionsText.Text = this.screenActionResult;
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(this.screenRecordingCancellation, cancellation))
            {
                this.screenRecordingCancellation = null;
            }
            this.UpdateScreenRecordingPlanPreview();
            this.RenderDeviceCapabilityCards();
        }
    }

    private void CancelScreenRecording()
    {
        if (this.screenRecordingCancellation is null)
        {
            return;
        }

        this.screenActionResult = this.S("Shell.Devices.ScreenRecording.Cancelling", "Cancelling screen recording...");
        this.nativeActionsText.Text = this.screenActionResult;
        this.screenRecordingCancellation.Cancel();
        this.UpdateScreenRecordingPlanPreview();
    }

    private async Task CaptureCameraPhotoAsync()
    {
        this.cameraActionResult = "Capturing camera photo...";
        this.nativeActionsText.Text = this.cameraActionResult;
        this.RenderDeviceCapabilityCards();
        var result = await this.appState.DeviceCapabilities.CaptureCameraPhotoAsync();
        this.cameraActionResult = result.Detail;
        this.nativeActionsText.Text = this.cameraActionResult;
        this.RenderDeviceCapabilityCards();
        if (result.Succeeded && !string.IsNullOrWhiteSpace(result.Path))
        {
            WindowsShell.OpenFileInExplorer(result.Path);
        }
    }

    private async Task SynthesizeSpeechAsync()
    {
        var text = this.textToSpeechInput.Text.Trim();
        if (text.Length == 0)
        {
            this.textToSpeechActionResult = this.S(
                "Shell.Devices.SystemSpeech.EmptyText",
                "Enter speech text before saving a clip.");
            this.nativeActionsText.Text = this.textToSpeechActionResult;
            this.RenderDeviceCapabilityCards();
            return;
        }

        var voiceId = this.textToSpeechVoiceInput.SelectedItem is XamlComboBoxItem { Tag: string selectedVoiceId }
            ? selectedVoiceId
            : null;
        var result = await this.appState.TextToSpeech.SynthesizeToFileAsync(
            new WindowsTextToSpeechRequest(text, voiceId, "reply"));
        this.latestTextToSpeechPath = result.Path;
        this.textToSpeechActionResult = result.Detail;
        this.nativeActionsText.Text = this.textToSpeechActionResult;
        this.RenderDeviceCapabilityCards();
    }

    private async Task OpenUriWithPolicyAsync(string? target, string destination)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            this.browserProxyActionResult = this.S("Shell.Devices.BrowserProxy.NoDashboard", "Gateway dashboard is not available yet.");
            this.nativeActionsText.Text = this.browserProxyActionResult;
            this.RenderDeviceCapabilityCards();
            return;
        }

        if (this.policyPreferences.BlockUnsafeUrls)
        {
            var evaluation = this.appState.UrlRisk.Evaluate(target);
            if (!evaluation.Allowed)
            {
                this.browserProxyActionResult = evaluation.Reason ?? target;
                this.nativeActionsText.Text = this.browserProxyActionResult;
                await this.RecordActivityAsync("browser-proxy", "Browser proxy URL blocked", this.browserProxyActionResult, destination, target);
                this.RenderDeviceCapabilityCards();
                return;
            }

            target = evaluation.NormalizedUrl ?? target;
        }

        await Launcher.LaunchUriAsync(new Uri(target));
        this.browserProxyActionResult = this.SF(
            "Shell.Devices.BrowserProxy.OpenedDashboard",
            "Opened {0}.",
            target);
        this.nativeActionsText.Text = this.browserProxyActionResult;
        await this.RecordActivityAsync("browser-proxy", "Browser proxy dashboard opened", this.browserProxyActionResult, destination, target);
        this.RenderDeviceCapabilityCards();
    }

    private void ShowNotification(
        string destination,
        string title,
        string message,
        WindowsNotificationKind kind = WindowsNotificationKind.Unknown)
    {
        var classification = this.notificationRuleEvaluator.Classify(
            kind,
            destination,
            this.notificationRulePreferences.Rules);
        this.appState.Notifications.Add(classification.Destination, title, message, classification.Category, kind);
        this.RenderNotificationActivity();
        _ = this.StoreNotificationHistoryAsync(classification, title, message);
        _ = this.RecordActivityAsync("notification", title, message, destination);
        if (this.trayHost is null)
        {
            this.notificationActionResult = "Tray host is not ready for notifications.";
            this.nativeActionsText.Text = this.notificationActionResult;
            this.RenderDeviceCapabilityCards();
            return;
        }

        this.trayHost.ShowNotification(title, message);
        this.notificationActionResult = "Notification sent.";
        this.nativeActionsText.Text = this.notificationActionResult;
        this.RenderDeviceCapabilityCards();
    }

    private async Task StoreNotificationHistoryAsync(
        WindowsNotificationClassification classification,
        string title,
        string message)
    {
        try
        {
            await this.appState.NotificationHistory.AddAsync(
                classification.Destination,
                title,
                message,
                this.notificationRulePreferences.HistoryRetentionCount,
                classification.Category,
                classification.Kind);
            _ = this.DispatcherQueue.TryEnqueue(() =>
            {
                this.RenderNotificationActivity();
                this.RenderLogsDiagnostics();
            });
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
        }
    }

    private void ShowOverlay(string title, string message)
    {
        this.overlayWindow?.Close();
        this.overlayWindow = new Window
        {
            Title = title,
            Content = new Border
            {
                Padding = new Thickness(24),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 20,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                        },
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                            Opacity = 0.84,
                        },
                    },
                },
            },
        };
        this.overlayWindow.Activate();
        this.overlayWindow.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(380, 160));
        if (this.overlayWindow.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
        }

        this.overlayActionResult = "Overlay shown.";
        this.nativeActionsText.Text = this.overlayActionResult;
        this.RenderDeviceCapabilityCards();
    }

    private void SyncHotkeyRegistration(bool enabled)
    {
        if (!enabled)
        {
            this.hotkeyService?.Unregister();
            return;
        }

        this.hotkeyService ??= new WindowsGlobalHotkeyService(() =>
            _ = this.DispatcherQueue.TryEnqueue(() =>
            {
                this.hotkeyActionResult = "Push-to-talk hotkey pressed.";
                this.nativeActionsText.Text = this.hotkeyActionResult;
                this.ShowOverlay("Push-to-talk", "Ctrl+Shift+Space was received by the Windows companion.");
                this.RenderDeviceCapabilityCards();
            }));
        try
        {
            this.hotkeyService.RegisterPushToTalkHotkey();
        }
        catch (Exception ex)
        {
            this.hotkeyActionResult = ex.Message;
            this.nativeActionsText.Text = this.hotkeyActionResult;
            this.RenderDeviceCapabilityCards();
        }
    }

    private async Task RunGatewayActionAsync(GatewayCliAction action)
    {
        // Render the immediate action result before the full refresh updates every panel.
        this.homeActivityText.Text = $"{action} started.";
        this.statusText.Text = $"{action} in progress...";
        await this.RecordActivityAsync("gateway", $"{action} started", $"{action} started.", WindowsNavigationDestination.Home);
        var result = await this.coordinator.RunGatewayActionAsync(action);
        this.RenderStatus(result.Status);
        this.homeActivityText.Text = this.coordinator.LastActivity ?? "";
        if (!result.Succeeded)
        {
            this.detailText.Text = result.Output;
        }
        await this.RecordActivityAsync(
            "gateway",
            $"{action} {(result.Succeeded ? "completed" : "failed")}",
            result.Succeeded ? this.coordinator.LastActivity ?? $"{action} completed." : result.Output,
            WindowsNavigationDestination.Home);
        await this.RefreshAllAsync();
    }

    private void RenderStatus(GatewayStatusSnapshot status)
    {
        this.coordinator.ApplyGatewayStatus(status);
        this.statusText.Text = $"Gateway: {status.State}";
        this.navigationGatewayStatusText.Text = $"Gateway: {status.State}";
        this.detailText.Text =
            $"Service installed: {status.ServiceInstalled}\n" +
            $"Reachable: {status.Reachable}\n" +
            $"Capability: {status.Capability}\n" +
            $"Dashboard: {status.DashboardUrl ?? "unknown"}\n" +
            $"Logs: {status.LogPath ?? "unknown"}";
        this.logsText.Text =
            $"Gateway logs: {status.LogPath ?? "unknown"}\n" +
            $"Service installed: {status.ServiceInstalled}\n" +
            $"Reachable: {status.Reachable}\n" +
            $"Capability: {status.Capability}";
        this.RenderLogsDiagnostics();
        this.RenderSettingsStorage();
        this.RenderHomeDashboard();
        this.UpdateTrayTooltip();
        var health = status.Reachable ? "reachable" : "unreachable";
        if (this.notificationPreferences.GatewayHealthAlerts &&
            this.lastNotifiedGatewayHealth is not null &&
            !string.Equals(this.lastNotifiedGatewayHealth, health, StringComparison.Ordinal))
        {
            this.ShowNotification(
                WindowsNavigationDestination.Home,
                "OpenClaw Gateway health",
                $"Gateway is {health}.",
                WindowsNotificationKind.GatewayHealth);
        }
        this.lastNotifiedGatewayHealth = health;
    }

    private static UIElement BuildOnboardingRow(OnboardingCheckResult check)
    {
        var stateText = new TextBlock
        {
            Text = check.State.ToString(),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = StateBrush(check.State),
        };
        var labelText = new TextBlock
        {
            Text = check.Label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        var detailText = new TextBlock
        {
            Text = check.Detail,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        };

        var row = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(96) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        row.Children.Add(stateText);
        var detail = new StackPanel { Spacing = 2 };
        detail.Children.Add(labelText);
        detail.Children.Add(detailText);
        Grid.SetColumn(detail, 1);
        row.Children.Add(detail);
        return row;
    }

    private void RenderHomeDashboard()
    {
        var summary = GatewayDashboardSummary.Create(
            this.coordinator.GatewayStatus,
            this.coordinator.RealtimeState,
            this.coordinator.OnboardingChecks,
            this.appState.Realtime.Authorization);
        this.homeGatewayStateText.Text = summary.GatewayState;
        this.homeGatewayHealthText.Text = summary.HealthState;
        this.homeConnectionStateText.Text = summary.ConnectionState;
        this.homeGatewayRows.Children.Clear();
        this.homeGatewayRows.Children.Add(BuildDashboardRow("Service", summary.ServiceState));
        this.homeGatewayRows.Children.Add(BuildDashboardRow("RPC", summary.Reachability));
        this.homeGatewayRows.Children.Add(BuildDashboardRow("Capability", summary.Capability));
        this.homeGatewayRows.Children.Add(BuildDashboardRow("Onboarding", summary.OnboardingHealth));
        this.homeGatewayRows.Children.Add(BuildDashboardRow("Dashboard", summary.DashboardUrl));
        this.homeGatewayRows.Children.Add(BuildDashboardRow("Logs", summary.LogPath));

        this.homeConnectionRows.Children.Clear();
        this.homeConnectionRows.Children.Add(BuildDashboardRow("Realtime", summary.ConnectionState));
        this.homeConnectionRows.Children.Add(BuildDashboardRow("Endpoint", this.gatewayUrlInput.Text));
        this.homeConnectionRows.Children.Add(BuildDashboardRow(
            "Last detail",
            this.coordinator.RealtimeReason ?? this.detailText.Text ?? "No connection detail yet."));
        var workflowSummary = OperatorWorkflowSummary.Create(
            this.latestApprovals,
            this.latestPairingRequests,
            this.coordinator.RealtimeState);
        this.homeOperatorRows.Children.Clear();
        this.homeOperatorRows.Children.Add(BuildDashboardRow(
            "Approvals",
            this.approvalsLoaded ? workflowSummary.ApprovalsStatus : "Not checked"));
        this.homeOperatorRows.Children.Add(BuildDashboardRow(
            "Pairing",
            this.pairingLoaded ? workflowSummary.PairingStatus : "Not checked"));
        this.homeOperatorRows.Children.Add(BuildDashboardRow("Readiness", workflowSummary.PairingReadiness));
        if (string.IsNullOrWhiteSpace(this.homeActivityText.Text))
        {
            this.homeActivityText.Text =
                "Recent activity will show Gateway lifecycle events, realtime events, approvals, and pairing requests as they arrive.";
        }
        this.RenderGuidedOnboardingActions();
        this.RenderNotificationActivity();
    }

    private void RenderNotificationActivity()
    {
        this.homeNotificationRows.Children.Clear();
        var entries = this.appState.NotificationHistory.Entries;
        if (entries.Count == 0)
        {
            this.homeNotificationRows.Children.Add(BuildDashboardRow("Latest", "No notifications sent yet."));
            return;
        }

        foreach (var entry in entries.Take(5))
        {
            this.homeNotificationRows.Children.Add(BuildTimestampSummaryRow(
                entry.CreatedAt,
                $"{entry.Kind}/{entry.Category}: {entry.Title}"));
        }
    }

    private void RenderGuidedOnboardingActions()
    {
        var browserProxyStatus = this.appState.BrowserProxy.CreateStatus(
            this.LoadCurrentShellPreferences(),
            this.coordinator.GatewayStatus);
        var plan = this.guidedOnboarding.CreatePlan(
            this.LoadCurrentShellPreferences(),
            this.coordinator.GatewayStatus,
            this.coordinator.RealtimeState,
            this.appState.Tunnel.Status,
            this.coordinator.OnboardingChecks,
            browserProxyStatus,
            this.appState.TextToSpeech.GetStatus());
        this.onboardingGuidedSummaryText.Text = plan.Summary;
        this.onboardingGuidedActions.Children.Clear();
        foreach (var action in plan.Actions)
        {
            var button = new XamlButton
            {
                Content = action.Title,
                HorizontalAlignment = XamlHorizontalAlignment.Left,
                Command = this.CreateCommand(async () => await this.RunGuidedActionAsync(action)),
            };
            AutomationProperties.SetName(button, action.Title);
            this.onboardingGuidedActions.Children.Add(BuildDashboardCard(
                null,
                BuildSettingsSection(
                    button,
                    new TextBlock
                    {
                        Text = action.Detail,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
                    },
                    BuildDashboardRow("Destination", this.appState.Navigation.PageTitle(action.Destination)))));
        }
    }

    private async Task RunGuidedActionAsync(WindowsGuidedAction action)
    {
        switch (action.Key)
        {
            case WindowsGuidedActionKey.InstallGateway:
                await this.RunGatewayActionAsync(GatewayCliAction.Install);
                break;
            case WindowsGuidedActionKey.StartGateway:
                await this.RunGatewayActionAsync(GatewayCliAction.Start);
                break;
            case WindowsGuidedActionKey.ConnectGateway:
                await this.ConnectRealtimeAsync();
                break;
            case WindowsGuidedActionKey.StartTunnel:
                await this.RunTunnelFromSettingsAsync();
                break;
            case WindowsGuidedActionKey.OpenSettings:
            case WindowsGuidedActionKey.OpenDevices:
            case WindowsGuidedActionKey.OpenLogs:
                this.ShowDestination(action.Destination);
                break;
            default:
                this.ShowDestination(action.Destination);
                break;
        }
    }

    private static UIElement BuildDashboardRow(string label, string value)
    {
        var row = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(112) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });
        var valueText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "unknown" : value,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        Grid.SetColumn(valueText, 1);
        row.Children.Add(valueText);
        return row;
    }

    private static UIElement BuildTimestampSummaryRow(DateTimeOffset createdAt, string summary)
    {
        var row = new StackPanel { Spacing = 4 };
        row.Children.Add(new TextBlock
        {
            Text = createdAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
            TextWrapping = TextWrapping.NoWrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });
        row.Children.Add(new TextBlock
        {
            Text = summary,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        return row;
    }

    private static UIElement BuildStackedDashboardRow(string label, string value)
    {
        var row = new StackPanel { Spacing = 4 };
        row.Children.Add(new TextBlock
        {
            Text = label,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });
        row.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "unknown" : value,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        return row;
    }

    private static string Plural(int count)
    {
        return count == 1 ? "" : "s";
    }

    private static Brush StateBrush(OnboardingCheckState state)
    {
        return state switch
        {
            OnboardingCheckState.Passed => ResourceBrush("SystemFillColorSuccessBrush"),
            OnboardingCheckState.Warning => ResourceBrush("SystemFillColorCautionBrush"),
            OnboardingCheckState.Failed => ResourceBrush("SystemFillColorCriticalBrush"),
            _ => ResourceBrush("TextFillColorPrimaryBrush"),
        };
    }
}
