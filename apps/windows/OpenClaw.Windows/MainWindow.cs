using System.Globalization;
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
    private static readonly SolidColorBrush SuccessBrush = new();
    private static readonly SolidColorBrush CautionBrush = new();
    private static readonly SolidColorBrush CriticalBrush = new();

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
    private readonly StackPanel homeOperatorRows = new() { Spacing = 8 };
    private readonly StackPanel chatMessages = new() { Spacing = 8 };
    private readonly TextBlock chatStateText = new();
    private readonly TextBlock chatSessionText = new();
    private readonly TextBlock chatEmptyText = new();
    private readonly StackPanel chatEventMessages = new() { Spacing = 8 };
    private readonly StackPanel chatEventVisibilityControls = new() { Spacing = 6 };
    private readonly TextBlock chatEventVisibilitySummaryText = new();
    private readonly List<GatewayRealtimeEvent> chatRealtimeEvents = [];
    private string? chatEventVisibilityControlSignature;
    private bool updatingSessionEventVisibilityControls;
    private readonly XamlButton chatRefreshButton = new();
    private readonly XamlButton chatSendButton = new();
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
    private readonly StackPanel logsDiagnosticsRows = new() { Spacing = 8 };
    private readonly StackPanel logsLocationCards = new() { Spacing = 12 };
    private readonly XamlTextBox rawLogsText = new();
    private readonly TextBlock logsText = new();
    private DateTimeOffset? lastGatewayStatusCheckedAt;
    private readonly TextBlock settingsText = new();
    private readonly StackPanel settingsStorageRows = new() { Spacing = 10 };
    private readonly XamlCheckBox openMainWindowOnLaunchInput = new();
    private readonly XamlCheckBox approvalAlertsInput = new();
    private readonly XamlCheckBox pairingAlertsInput = new();
    private readonly XamlCheckBox gatewayHealthAlertsInput = new();
    private readonly XamlCheckBox devicePermissionAlertsInput = new();
    private readonly XamlCheckBox settingsVoiceControlsInput = new();
    private readonly XamlCheckBox settingsGlobalHotkeyInput = new();
    private readonly XamlComboBox themePreferenceInput = new();
    private readonly XamlTextBox chatInput = new() { AcceptsReturn = true, Height = 88, TextWrapping = TextWrapping.Wrap };
    private readonly XamlTextBox gatewayUrlInput = new();
    private readonly XamlPasswordBox gatewayTokenInput = new();
    private readonly XamlTextBox chatSessionInput = new();
    private bool openMainWindowOnLaunch = AppPreferences.Default.OpenMainWindowOnLaunch;
    private WindowsThemePreference themePreference = AppPreferences.Default.ThemePreference;
    private SessionEventVisibilityPreferences sessionEventVisibility = AppPreferences.Default.SessionEventVisibility;
    private WindowsNotificationPreferences notificationPreferences = WindowsNotificationPreferences.Default;
    private bool voiceControlsEnabled;
    private bool globalHotkeyEnabled;
    private IReadOnlyList<WindowsDevicePermissionStatus> latestDevicePermissionStatuses = [];
    private string mediaDeviceSummary = "Media devices have not been checked yet.";
    private string screenActionResult = "No screen capture run yet.";
    private string cameraActionResult = "No camera capture run yet.";
    private string microphoneActionResult = "Voice controls have not been saved yet.";
    private string notificationActionResult = "No notification sent yet.";
    private string hotkeyActionResult = "Global hotkey preference has not been saved yet.";
    private string overlayActionResult = "No overlay shown yet.";
    private WindowsTrayHost? trayHost;
    private WindowsGlobalHotkeyService? hotkeyService;
    private Window? overlayWindow;
    private NavigationView? navigationView;
    private bool exitRequested;
    private bool shutdownStarted;

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
        this.Title = "OpenClaw";
        this.appState.Realtime.StateChanged += this.OnRealtimeStateChanged;
        this.appState.Realtime.EventReceived += this.OnRealtimeEventReceived;
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
            UpdateThemeBrushes(ResolveBrushTheme(root, preference));
        }
    }

    public void AttachTrayHost(WindowsTrayHost trayHost)
    {
        this.trayHost = trayHost;
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
            PaneTitle = "OpenClaw",
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
        UpdateThemeBrushes(ResolveBrushTheme(root, this.themePreference));
        root.ActualThemeChanged += (_, _) => UpdateThemeBrushes(root.ActualTheme);

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
        root.Children.Add(header);

        Grid.SetRow(content, 1);
        root.Children.Add(content);
        return root;
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
            WindowsNavigationDestination.Home => this.BuildPage(WindowsNavigationService.PageTitle(tag), Scrollable(this.BuildHomeDashboardPanel())),
            WindowsNavigationDestination.Sessions => this.BuildPage(WindowsNavigationService.PageTitle(tag), Scrollable(this.BuildChatPanel())),
            WindowsNavigationDestination.Approvals => this.BuildPage(WindowsNavigationService.PageTitle(tag), Scrollable(this.BuildApprovalsPanel())),
            WindowsNavigationDestination.Pairing => this.BuildPage(WindowsNavigationService.PageTitle(tag), Scrollable(this.BuildPairingPanel())),
            WindowsNavigationDestination.Devices => this.BuildPage(WindowsNavigationService.PageTitle(tag), Scrollable(this.BuildDevicesPanel())),
            WindowsNavigationDestination.Logs => this.BuildPage(WindowsNavigationService.PageTitle(tag), Scrollable(this.BuildLogsPanel())),
            WindowsNavigationDestination.Settings => this.BuildPage(WindowsNavigationService.PageTitle(tag), Scrollable(this.BuildSettingsPanel())),
            _ => this.BuildPage("Home", Scrollable(this.BuildHomeDashboardPanel())),
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

    private UIElement BuildChatPanel()
    {
        var panel = new StackPanel { Spacing = 16 };
        this.chatStateText.TextWrapping = TextWrapping.Wrap;
        this.chatStateText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        this.chatSessionText.TextWrapping = TextWrapping.Wrap;
        this.chatSessionText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");
        this.chatEmptyText.Text = "No messages in this session yet.";
        this.chatEmptyText.TextWrapping = TextWrapping.Wrap;
        this.chatEmptyText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");
        this.chatInput.PlaceholderText = "Message the active OpenClaw session";
        AutomationProperties.SetName(this.chatInput, "Message the active OpenClaw session");

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
            Text = "Conversation",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        headerText.Children.Add(this.chatSessionText);
        header.Children.Add(headerText);

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        this.chatRefreshButton.Content = "Refresh";
        this.chatRefreshButton.AccessKey = "R";
        this.chatRefreshButton.Command = this.CreateCommand(async () => await this.RefreshChatAsync());
        AutomationProperties.SetName(this.chatRefreshButton, "Refresh session messages");
        buttons.Children.Add(this.chatRefreshButton);
        this.chatSendButton.Content = "Send";
        this.chatSendButton.AccessKey = "S";
        this.chatSendButton.Command = this.CreateCommand(async () => await this.SendChatAsync());
        this.chatSendButton.KeyboardAccelerators.Add(new KeyboardAccelerator
        {
            Key = VirtualKey.Enter,
            Modifiers = VirtualKeyModifiers.Control,
        });
        AutomationProperties.SetName(this.chatSendButton, "Send message");
        buttons.Children.Add(this.chatSendButton);
        Grid.SetColumn(buttons, 1);
        header.Children.Add(buttons);
        panel.Children.Add(header);

        panel.Children.Add(BuildDashboardCard("Session state", this.chatStateText));
        panel.Children.Add(BuildDashboardCard("Event visibility", this.BuildChatEventVisibilityPanel()));

        var conversation = new Border
        {
            Padding = new Thickness(16),
            MinHeight = 320,
            Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = ResourceCornerRadius("OverlayCornerRadius"),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    this.chatEmptyText,
                    this.chatMessages,
                    this.chatEventMessages,
                },
            },
        };
        panel.Children.Add(conversation);
        panel.Children.Add(BuildDashboardCard("Composer", this.chatInput));
        this.RenderChatWorkspace();
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
        this.themePreferenceInput.Items.Clear();
        this.themePreferenceInput.Items.Add(CreateThemePreferenceItem("System", WindowsThemePreference.System));
        this.themePreferenceInput.Items.Add(CreateThemePreferenceItem("Light", WindowsThemePreference.Light));
        this.themePreferenceInput.Items.Add(CreateThemePreferenceItem("Dark", WindowsThemePreference.Dark));
        this.themePreferenceInput.SelectionChanged += this.OnThemePreferenceSelectionChanged;
        this.SelectThemePreference(this.themePreference);
        AutomationProperties.SetName(this.themePreferenceInput, "App theme");
        this.settingsText.TextWrapping = TextWrapping.Wrap;
        this.settingsText.Foreground = ResourceBrush("TextFillColorSecondaryBrush");

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
            this.settingsVoiceControlsInput,
            "Enable voice controls",
            value => this.voiceControlsEnabled = value);
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
            BuildSettingsField("Theme", "Choose System, Light, or Dark for the Windows companion.", this.themePreferenceInput))));
        panel.Children.Add(BuildDashboardCard("Startup", BuildSettingsSection(
            this.openMainWindowOnLaunchInput,
            BuildReservedSettingsRow("Autostart", "Reserved", "Future tray startup preference."))));
        panel.Children.Add(BuildDashboardCard("Notifications", BuildSettingsSection(
            this.approvalAlertsInput,
            this.pairingAlertsInput,
            this.gatewayHealthAlertsInput,
            this.devicePermissionAlertsInput)));
        panel.Children.Add(BuildDashboardCard("Devices", BuildSettingsSection(
            this.settingsVoiceControlsInput,
            this.settingsGlobalHotkeyInput)));
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

    private static UIElement BuildSettingsField(string label, string detail, Control input)
    {
        var field = new StackPanel { Spacing = 4 };
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
        return field;
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

    private static void ConfigureSettingsToggle(XamlCheckBox toggle, string label, Action<bool> update)
    {
        toggle.Content = label;
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
        this.RenderChatWorkspace();
    }

    private void OnThemePreferenceSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (this.themePreferenceInput.SelectedItem is XamlComboBoxItem { Tag: WindowsThemePreference preference })
        {
            this.ApplyThemePreference(preference);
        }
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

    private static void UpdateThemeBrushes(ElementTheme theme)
    {
        if (theme == ElementTheme.Dark)
        {
            AppBackgroundBrush.Color = Color.FromArgb(255, 15, 15, 15);
            CardBackgroundBrush.Color = Color.FromArgb(255, 31, 31, 31);
            CardStrokeBrush.Color = Color.FromArgb(255, 62, 62, 62);
            LayerFillBrush.Color = Color.FromArgb(255, 24, 24, 24);
            TextPrimaryBrush.Color = Color.FromArgb(255, 255, 255, 255);
            TextSecondaryBrush.Color = Color.FromArgb(255, 196, 196, 196);
            SuccessBrush.Color = Color.FromArgb(255, 108, 203, 95);
            CautionBrush.Color = Color.FromArgb(255, 249, 199, 79);
            CriticalBrush.Color = Color.FromArgb(255, 255, 107, 107);
            return;
        }

        AppBackgroundBrush.Color = Color.FromArgb(255, 247, 247, 247);
        CardBackgroundBrush.Color = Color.FromArgb(255, 255, 255, 255);
        CardStrokeBrush.Color = Color.FromArgb(255, 229, 231, 235);
        LayerFillBrush.Color = Color.FromArgb(255, 248, 248, 248);
        TextPrimaryBrush.Color = Color.FromArgb(255, 26, 26, 26);
        TextSecondaryBrush.Color = Color.FromArgb(255, 96, 96, 96);
        SuccessBrush.Color = Color.FromArgb(255, 24, 128, 56);
        CautionBrush.Color = Color.FromArgb(255, 151, 104, 0);
        CriticalBrush.Color = Color.FromArgb(255, 185, 28, 28);
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
        panel.Children.Add(BuildDashboardCard("Raw log preview", this.rawLogsText));
        this.RenderLogsDiagnostics();
        return panel;
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
        return new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
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
            this.RenderLogsDiagnostics();
            this.RenderChatWorkspace();
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
            this.RenderChatWorkspace();
        });
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
        this.hotkeyService?.Dispose();
        this.overlayWindow?.Close();
        try
        {
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
            this.lastGatewayStatusCheckedAt = preferences.LastStatusCheckedAt;
            this.gatewayUrlInput.Text = preferences.GatewayUrl;
            this.gatewayTokenInput.Password = preferences.GatewayToken ?? "";
            this.chatSessionInput.Text = preferences.ChatSessionKey;
            this.openMainWindowOnLaunch = preferences.OpenMainWindowOnLaunch;
            this.ApplyThemePreference(preferences.ThemePreference);
            this.sessionEventVisibility = preferences.SessionEventVisibility.WithObservedEvents(this.chatRealtimeEvents);
            this.notificationPreferences = preferences.NotificationPreferences;
            this.voiceControlsEnabled = preferences.VoiceControlsEnabled;
            this.globalHotkeyEnabled = preferences.GlobalHotkeyEnabled;
            this.openMainWindowOnLaunchInput.IsChecked = preferences.OpenMainWindowOnLaunch;
            this.SelectThemePreference(preferences.ThemePreference);
            this.approvalAlertsInput.IsChecked = preferences.NotificationPreferences.ApprovalAlerts;
            this.pairingAlertsInput.IsChecked = preferences.NotificationPreferences.PairingAlerts;
            this.gatewayHealthAlertsInput.IsChecked = preferences.NotificationPreferences.GatewayHealthAlerts;
            this.devicePermissionAlertsInput.IsChecked = preferences.NotificationPreferences.DevicePermissionAlerts;
            this.settingsVoiceControlsInput.IsChecked = preferences.VoiceControlsEnabled;
            this.settingsGlobalHotkeyInput.IsChecked = preferences.GlobalHotkeyEnabled;
            this.RenderHomeDashboard();
            this.RenderSettingsSummary(preferences);
            this.RenderSettingsStorage();
            this.RenderLogsDiagnostics();
            await this.RefreshDeviceCapabilitiesAsync();
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
        await this.RefreshChatAsync();
        await this.RefreshApprovalsAsync();
        await this.RefreshPairingAsync();
    }

    private async Task RefreshChatAsync()
    {
        try
        {
            var preferences = await this.appState.Preferences.LoadAsync();
            var messages = await this.appState.Realtime.LoadChatHistoryAsync(preferences.ChatSessionKey);
            this.chatRealtimeEvents.Clear();
            this.chatState.ApplyMessages(messages, this.appState.Realtime.State);
            this.RenderChatWorkspace(preferences.ChatSessionKey);
        }
        catch (Exception ex)
        {
            this.chatState.ApplyFailure(ex);
            this.RenderChatWorkspace();
            throw;
        }
    }

    private async Task SendChatAsync()
    {
        var message = this.chatInput.Text.Trim();
        if (message.Length == 0)
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
            $"Gateway status: {summary.GatewayStatus}\n" +
            $"Last error: {summary.LastError}\n" +
            $"Last refresh: {summary.LastRefresh}";
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
        var activeSession = string.IsNullOrWhiteSpace(sessionKey)
            ? string.IsNullOrWhiteSpace(this.chatSessionInput.Text)
                ? AppPreferences.Default.ChatSessionKey
                : this.chatSessionInput.Text.Trim()
            : sessionKey;
        this.chatSessionText.Text = $"Session: {activeSession}";
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

        var visibleEvents = SessionEventVisibility.Filter(
            this.chatRealtimeEvents,
            this.sessionEventVisibility,
            activeSession);
        var hiddenEventCount = SessionEventVisibility.CountHidden(
            this.chatRealtimeEvents,
            this.sessionEventVisibility,
            activeSession);

        this.chatEmptyText.Text = hiddenEventCount > 0
            ? $"No visible realtime events match the current filters. {hiddenEventCount} hidden event{Plural(hiddenEventCount)} can be restored from Event visibility."
            : "No messages in this session yet.";
        this.chatEmptyText.Visibility =
            this.chatState.Messages.Count == 0 && visibleEvents.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        this.chatMessages.Children.Clear();
        foreach (var message in this.chatState.Messages)
        {
            this.chatMessages.Children.Add(BuildChatMessageRow(message));
        }

        this.chatEventMessages.Children.Clear();
        foreach (var @event in visibleEvents)
        {
            this.chatEventMessages.Children.Add(BuildChatEventRow(@event));
        }
        this.RenderChatEventVisibilityControls(activeSession, hiddenEventCount);
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
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new TextBlock
        {
            Text = role,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush"),
        });
        body.Children.Add(new TextBlock
        {
            Text = message.Text,
            TextWrapping = TextWrapping.Wrap,
        });

        return new Border
        {
            Padding = new Thickness(12),
            Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = ResourceCornerRadius("OverlayCornerRadius"),
            Child = body,
        };
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

    private async Task RefreshApprovalsAsync()
    {
        this.latestApprovals = await this.appState.Realtime.ListApprovalsAsync();
        this.approvalsLoaded = true;
        if (this.notificationPreferences.ApprovalAlerts &&
            this.latestApprovals.Count > 0 &&
            this.latestApprovals.Count != this.lastNotifiedApprovalCount)
        {
            this.ShowNotification(
                WindowsNavigationDestination.Approvals,
                "OpenClaw approval",
                $"{this.latestApprovals.Count} approval request{Plural(this.latestApprovals.Count)} pending.");
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

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        var allowButton = new XamlButton
        {
            Content = "Allow once",
            AccessKey = "A",
            Command = this.CreateCommand(async () =>
            {
                await this.appState.Realtime.ResolveApprovalAsync(approval.Id, "allow-once");
                await this.RefreshApprovalsAsync();
            }),
        };
        AutomationProperties.SetName(allowButton, $"Allow approval {approval.Id} once");
        buttons.Children.Add(allowButton);
        var denyButton = new XamlButton
        {
            Content = "Deny",
            AccessKey = "D",
            Command = this.CreateCommand(async () =>
            {
                await this.appState.Realtime.ResolveApprovalAsync(approval.Id, "deny");
                await this.RefreshApprovalsAsync();
            }),
        };
        AutomationProperties.SetName(denyButton, $"Deny approval {approval.Id}");
        buttons.Children.Add(denyButton);
        body.Children.Add(buttons);
        return BuildDashboardCard(null, body);
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
                $"{this.latestPairingRequests.Count} pairing request{Plural(this.latestPairingRequests.Count)} pending.");
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
            $"Last status: {preferences.LastStatus ?? "unknown"}\n" +
            $"Last checked: {preferences.LastStatusCheckedAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "never"}\n" +
            $"Device token cached: {!string.IsNullOrWhiteSpace(preferences.DeviceToken)}\n" +
            $"Voice controls: {preferences.VoiceControlsEnabled}\n" +
            $"Global hotkey: {preferences.GlobalHotkeyEnabled}\n" +
            $"Approval alerts: {preferences.NotificationPreferences.ApprovalAlerts}\n" +
            $"Pairing alerts: {preferences.NotificationPreferences.PairingAlerts}\n" +
            $"Gateway health alerts: {preferences.NotificationPreferences.GatewayHealthAlerts}\n" +
            $"Device permission alerts: {preferences.NotificationPreferences.DevicePermissionAlerts}";
    }

    private void RenderSettingsStorage()
    {
        this.settingsStorageRows.Children.Clear();
        this.settingsStorageRows.Children.Add(BuildDashboardRow("Preferences", this.appState.Preferences.Path));
        this.settingsStorageRows.Children.Add(BuildDashboardRow("App crash log", CrashLog.Path));
        this.settingsStorageRows.Children.Add(BuildDashboardRow("Gateway log", this.coordinator.LogPath ?? "unknown"));
        this.settingsStorageRows.Children.Add(BuildReservedSettingsRow(
            "Minimize to tray",
            "Reserved",
            "Future app-local tray window behavior."));
        this.settingsStorageRows.Children.Add(BuildReservedSettingsRow(
            "Tray quick actions",
            "Reserved",
            "Future app-local tray action selection."));
    }

    private async Task SaveSettingsAsync()
    {
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
            SessionEventVisibility = this.sessionEventVisibility.WithObservedEvents(this.chatRealtimeEvents),
            NotificationPreferences = this.notificationPreferences,
            VoiceControlsEnabled = this.voiceControlsEnabled,
            GlobalHotkeyEnabled = this.globalHotkeyEnabled,
        });
        await this.RefreshAllAsync();
    }

    private async Task RefreshDeviceCapabilitiesAsync()
    {
        var preferences = await this.appState.Preferences.LoadAsync();
        this.notificationPreferences = preferences.NotificationPreferences;
        this.voiceControlsEnabled = preferences.VoiceControlsEnabled;
        this.globalHotkeyEnabled = preferences.GlobalHotkeyEnabled;
        this.approvalAlertsInput.IsChecked = preferences.NotificationPreferences.ApprovalAlerts;
        this.pairingAlertsInput.IsChecked = preferences.NotificationPreferences.PairingAlerts;
        this.gatewayHealthAlertsInput.IsChecked = preferences.NotificationPreferences.GatewayHealthAlerts;
        this.devicePermissionAlertsInput.IsChecked = preferences.NotificationPreferences.DevicePermissionAlerts;
        this.settingsVoiceControlsInput.IsChecked = preferences.VoiceControlsEnabled;
        this.settingsGlobalHotkeyInput.IsChecked = preferences.GlobalHotkeyEnabled;
        this.SyncHotkeyRegistration(preferences.GlobalHotkeyEnabled);
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
                $"Unavailable: {string.Join(", ", unavailableCapabilities)}.");
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
        this.deviceCapabilityCards.Children.Add(this.BuildDeviceCapabilityCard(
            DeviceCapabilityPresentation.Create("Screen", this.latestDevicePermissionStatuses, this.screenActionResult),
            [
                this.DeviceActionButton("Screen", "Capture primary screen", async () => await this.CaptureScreenAsync()),
                this.DeviceActionButton("Record", "Record screen frame sequence", async () => await this.CaptureScreenFramesAsync()),
            ]));
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
        this.deviceCapabilityCards.Children.Add(this.BuildDeviceCapabilityCard(
            DeviceCapabilityPresentation.Create("Overlays", this.latestDevicePermissionStatuses, this.overlayActionResult),
            [this.DeviceActionButton("Overlay", "Show test overlay", () =>
            {
                this.ShowOverlay("OpenClaw overlay", "Native Windows overlays are available.");
                return Task.CompletedTask;
            })]));
    }

    private UIElement BuildDeviceCapabilityCard(
        DeviceCapabilityPresentation presentation,
        IEnumerable<UIElement> actions)
    {
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(new TextBlock
        {
            Text = presentation.Capability,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        body.Children.Add(BuildDashboardRow("Permission", presentation.State));
        body.Children.Add(BuildDashboardRow("Detail", presentation.Detail));
        body.Children.Add(BuildDashboardRow("Last action", presentation.LastAction));
        body.Children.Add(BuildDashboardRow("Repair", presentation.RepairGuidance));
        if (presentation.Capability is "Camera" or "Microphone")
        {
            body.Children.Add(BuildDashboardRow("Devices", this.mediaDeviceSummary));
        }

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

    private async Task CaptureScreenFramesAsync()
    {
        this.screenActionResult = "Recording screen frame sequence...";
        this.nativeActionsText.Text = this.screenActionResult;
        this.RenderDeviceCapabilityCards();
        var captures = await Task.Run(() => this.appState.DeviceCapabilities.CaptureScreenFrameSequence());
        var successful = captures.Where(capture => capture.Succeeded).ToArray();
        this.screenActionResult = $"Captured {successful.Length} screen frames in {this.appState.DeviceCapabilities.CaptureRoot}";
        this.nativeActionsText.Text = this.screenActionResult;
        this.RenderDeviceCapabilityCards();
        if (successful.Length > 0)
        {
            WindowsShell.OpenFileInExplorer(this.appState.DeviceCapabilities.CaptureRoot);
        }
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

    private void ShowNotification(string destination, string title, string message)
    {
        this.appState.Notifications.Add(WindowsNavigationService.Normalize(destination), title, message);
        this.RenderNotificationActivity();
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
        var result = await this.coordinator.RunGatewayActionAsync(action);
        this.RenderStatus(result.Status);
        this.homeActivityText.Text = this.coordinator.LastActivity ?? "";
        if (!result.Succeeded)
        {
            this.detailText.Text = result.Output;
        }
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
        var health = status.Reachable ? "reachable" : "unreachable";
        if (this.notificationPreferences.GatewayHealthAlerts &&
            this.lastNotifiedGatewayHealth is not null &&
            !string.Equals(this.lastNotifiedGatewayHealth, health, StringComparison.Ordinal))
        {
            this.ShowNotification(
                WindowsNavigationDestination.Home,
                "OpenClaw Gateway health",
                $"Gateway is {health}.");
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
        this.RenderNotificationActivity();
    }

    private void RenderNotificationActivity()
    {
        this.homeNotificationRows.Children.Clear();
        var entries = this.appState.Notifications.Entries;
        if (entries.Count == 0)
        {
            this.homeNotificationRows.Children.Add(BuildDashboardRow("Latest", "No notifications sent yet."));
            return;
        }

        foreach (var entry in entries.Take(5))
        {
            this.homeNotificationRows.Children.Add(BuildDashboardRow(
                entry.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                $"{entry.Title}: {entry.Message}"));
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
