using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Windows.Native;
using XamlButton = Microsoft.UI.Xaml.Controls.Button;
using XamlCheckBox = Microsoft.UI.Xaml.Controls.CheckBox;
using XamlHorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using XamlOrientation = Microsoft.UI.Xaml.Controls.Orientation;
using XamlPasswordBox = Microsoft.UI.Xaml.Controls.PasswordBox;
using XamlTextBox = Microsoft.UI.Xaml.Controls.TextBox;

namespace OpenClaw.Windows;

public sealed class MainWindow : Window
{
    private readonly WindowsCompanionState appState;
    private readonly Dictionary<string, UIElement> navigationPages = new();
    private readonly ContentControl navigationContent = new();
    private readonly TextBlock navigationGatewayStatusText = new();
    private readonly TextBlock statusText = new();
    private readonly TextBlock detailText = new();
    private readonly TextBlock commandErrorText = new();
    private readonly StackPanel onboardingList = new() { Spacing = 6 };
    private readonly StackPanel chatMessages = new() { Spacing = 8 };
    private readonly StackPanel approvalsList = new() { Spacing = 8 };
    private readonly StackPanel pairingList = new() { Spacing = 8 };
    private readonly StackPanel deviceStatusList = new() { Spacing = 6 };
    private readonly StackPanel mediaDevicesList = new() { Spacing = 6 };
    private readonly TextBlock nativeActionsText = new();
    private readonly TextBlock logsText = new();
    private readonly TextBlock settingsText = new();
    private readonly XamlTextBox chatInput = new() { AcceptsReturn = true, Height = 88, TextWrapping = TextWrapping.Wrap };
    private readonly XamlTextBox gatewayUrlInput = new();
    private readonly XamlPasswordBox gatewayTokenInput = new();
    private readonly XamlTextBox chatSessionInput = new();
    private readonly XamlCheckBox voiceControlsToggle = new() { Content = "Enable voice controls" };
    private readonly XamlCheckBox globalHotkeyToggle = new() { Content = "Register Ctrl+Shift+Space push-to-talk hotkey" };
    private WindowsTrayHost? trayHost;
    private WindowsGlobalHotkeyService? hotkeyService;
    private Window? overlayWindow;
    private string? logPath;

    public MainWindow(WindowsCompanionState appState)
    {
        this.appState = appState;
        this.Title = "OpenClaw";
        this.appState.Realtime.StateChanged += this.OnRealtimeStateChanged;
        this.appState.Realtime.EventReceived += this.OnRealtimeEventReceived;
        this.Closed += this.OnClosed;
        this.Content = this.BuildContent();
    }

    public void AttachTrayHost(WindowsTrayHost trayHost)
    {
        this.trayHost = trayHost;
    }

    public void ShowShell()
    {
        this.Activate();
    }

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
        };
        navigation.MenuItems.Add(CreateNavigationItem("Home", "home", "\uE80F"));
        navigation.MenuItems.Add(CreateNavigationItem("Sessions", "sessions", "\uE8BD"));
        navigation.MenuItems.Add(CreateNavigationItem("Approvals", "approvals", "\uE73E"));
        navigation.MenuItems.Add(CreateNavigationItem("Pairing", "pairing", "\uE71B"));
        navigation.MenuItems.Add(CreateNavigationItem("Devices", "devices", "\uE722"));
        navigation.MenuItems.Add(CreateNavigationItem("Logs", "logs", "\uE8A5"));
        navigation.SelectionChanged += this.OnNavigationSelectionChanged;

        if (navigation.MenuItems.FirstOrDefault() is NavigationViewItem homeItem)
        {
            navigation.SelectedItem = homeItem;
            this.ShowNavigationPage(homeItem);
        }

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };
        root.Children.Add(this.commandErrorText);
        Grid.SetRow(navigation, 1);
        root.Children.Add(navigation);

        _ = this.RefreshAllAsync();
        return root;
    }

    private UIElement BuildPage(string title, FrameworkElement content)
    {
        var root = new Grid
        {
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
            this.navigationContent.Content = this.GetNavigationPage("settings");
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
        this.navigationContent.Content = this.GetNavigationPage(tag ?? "home");
    }

    private UIElement GetNavigationPage(string tag)
    {
        if (this.navigationPages.TryGetValue(tag, out var page))
        {
            return page;
        }

        page = tag switch
        {
            "home" => this.BuildPage("Home", Scrollable(this.BuildStatusPanel())),
            "sessions" => this.BuildPage("Sessions", Scrollable(this.BuildChatPanel())),
            "approvals" => this.BuildPage("Approvals", Scrollable(this.BuildApprovalsPanel())),
            "pairing" => this.BuildPage("Pairing", Scrollable(this.BuildPairingPanel())),
            "devices" => this.BuildPage("Devices", Scrollable(this.BuildDevicesPanel())),
            "logs" => this.BuildPage("Logs", Scrollable(this.BuildLogsPanel())),
            "settings" => this.BuildPage("Settings", Scrollable(this.BuildSettingsPanel())),
            _ => this.BuildPage("Home", Scrollable(this.BuildStatusPanel())),
        };
        this.navigationPages[tag] = page;
        return page;
    }

    private UIElement BuildStatusPanel()
    {
        var panel = new StackPanel { Spacing = 14 };
        this.statusText.Text = "Checking gateway...";
        this.statusText.FontSize = 20;
        this.statusText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        panel.Children.Add(this.statusText);
        panel.Children.Add(this.detailText);

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
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
                if (!string.IsNullOrWhiteSpace(this.logPath))
                {
                    WindowsShell.OpenFileInExplorer(this.logPath);
                }
                return Task.CompletedTask;
            }),
        });
        panel.Children.Add(buttons);

        panel.Children.Add(new TextBlock
        {
            Text = "Onboarding",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 18, 0, 0),
        });
        panel.Children.Add(this.onboardingList);
        return panel;
    }

    private UIElement BuildChatPanel()
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Chat",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        panel.Children.Add(this.chatMessages);
        panel.Children.Add(this.chatInput);
        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(new XamlButton
        {
            Content = "Refresh",
            Command = this.CreateCommand(async () => await this.RefreshChatAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Send",
            Command = this.CreateCommand(async () => await this.SendChatAsync()),
        });
        panel.Children.Add(buttons);
        return panel;
    }

    private UIElement BuildApprovalsPanel()
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Approvals",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        panel.Children.Add(new XamlButton
        {
            Content = "Refresh",
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            Command = this.CreateCommand(async () => await this.RefreshApprovalsAsync()),
        });
        panel.Children.Add(this.approvalsList);
        return panel;
    }

    private UIElement BuildPairingPanel()
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Pairing",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        panel.Children.Add(new XamlButton
        {
            Content = "Refresh",
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            Command = this.CreateCommand(async () => await this.RefreshPairingAsync()),
        });
        panel.Children.Add(this.pairingList);
        return panel;
    }

    private UIElement BuildDevicesPanel()
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Devices",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        panel.Children.Add(this.deviceStatusList);
        panel.Children.Add(new TextBlock
        {
            Text = "Media devices",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        panel.Children.Add(this.mediaDevicesList);
        panel.Children.Add(this.voiceControlsToggle);
        panel.Children.Add(this.globalHotkeyToggle);

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(new XamlButton
        {
            Content = "Refresh",
            Command = this.CreateCommand(async () => await this.RefreshDeviceCapabilitiesAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Screen",
            Command = this.CreateCommand(async () => await this.CaptureScreenAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Record",
            Command = this.CreateCommand(async () => await this.CaptureScreenFramesAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Camera",
            Command = this.CreateCommand(async () => await this.CaptureCameraPhotoAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Notify",
            Command = this.CreateCommand(() =>
            {
                this.ShowNotification("OpenClaw", "Windows companion notifications are available.");
                return Task.CompletedTask;
            }),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Overlay",
            Command = this.CreateCommand(() =>
            {
                this.ShowOverlay("OpenClaw overlay", "Native Windows overlays are available.");
                return Task.CompletedTask;
            }),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Save toggles",
            Command = this.CreateCommand(async () => await this.SaveDevicePreferencesAsync()),
        });
        panel.Children.Add(buttons);
        panel.Children.Add(this.nativeActionsText);
        return panel;
    }

    private UIElement BuildSettingsPanel()
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Settings",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        panel.Children.Add(new TextBlock { Text = "Gateway URL" });
        panel.Children.Add(this.gatewayUrlInput);
        panel.Children.Add(new TextBlock { Text = "Gateway token" });
        panel.Children.Add(this.gatewayTokenInput);
        panel.Children.Add(new TextBlock { Text = "Chat session" });
        panel.Children.Add(this.chatSessionInput);
        panel.Children.Add(this.settingsText);
        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(new XamlButton
        {
            Content = "Save",
            Command = this.CreateCommand(async () => await this.SaveSettingsAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Refresh",
            Command = this.CreateCommand(async () => await this.RefreshAllAsync()),
        });
        panel.Children.Add(buttons);
        return panel;
    }

    private UIElement BuildLogsPanel()
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Logs",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        this.logsText.Text = "Gateway log path has not been loaded yet.";
        this.logsText.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(this.logsText);

        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(new XamlButton
        {
            Content = "Refresh",
            Command = this.CreateCommand(async () => await this.RefreshAllAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Open Logs",
            Command = this.CreateCommand(() =>
            {
                if (!string.IsNullOrWhiteSpace(this.logPath))
                {
                    WindowsShell.OpenFileInExplorer(this.logPath);
                }

                return Task.CompletedTask;
            }),
        });
        panel.Children.Add(buttons);
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
        return new RelayCommand(async () =>
        {
            this.ClearCommandError();
            await execute();
        }, this.ReportCommandError);
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
        });
    }

    private void OnRealtimeStateChanged(GatewayRealtimeState state, string? reason)
    {
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            this.statusText.Text = $"Gateway: {state}";
            this.navigationGatewayStatusText.Text = $"Gateway: {state}";
            if (!string.IsNullOrWhiteSpace(reason))
            {
                this.detailText.Text = reason;
            }
        });
    }

    private void OnRealtimeEventReceived(GatewayRealtimeEvent @event)
    {
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            this.chatMessages.Children.Add(new TextBlock
            {
                Text = $"event:{@event.Name} {@event.Payload?.ToString() ?? ""}",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.76,
            });
        });
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
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
        try
        {
            try
            {
                var status = await this.appState.Gateway.RefreshStatusAsync();
                this.RenderStatus(status);
            }
            catch (Exception ex)
            {
                this.statusText.Text = "Gateway status unavailable";
                this.detailText.Text = ex.Message;
            }

            var checks = await this.appState.OnboardingChecks.RunAsync();
            this.onboardingList.Children.Clear();
            foreach (var check in checks)
            {
                this.onboardingList.Children.Add(new TextBlock
                {
                    Text = $"{check.Label}: {check.State} - {check.Detail}",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(check.State == OnboardingCheckState.Failed ? Microsoft.UI.Colors.Firebrick : Microsoft.UI.Colors.Black),
                });
            }

            var preferences = await this.appState.Preferences.LoadAsync();
            this.gatewayUrlInput.Text = preferences.GatewayUrl;
            this.gatewayTokenInput.Password = preferences.GatewayToken ?? "";
            this.chatSessionInput.Text = preferences.ChatSessionKey;
            this.voiceControlsToggle.IsChecked = preferences.VoiceControlsEnabled;
            this.globalHotkeyToggle.IsChecked = preferences.GlobalHotkeyEnabled;
            this.settingsText.Text =
                $"Open main window on launch: {preferences.OpenMainWindowOnLaunch}\n" +
                $"Last status: {preferences.LastStatus ?? "unknown"}\n" +
                $"Last checked: {preferences.LastStatusCheckedAt?.ToLocalTime().ToString("g") ?? "never"}\n" +
                $"Device token cached: {!string.IsNullOrWhiteSpace(preferences.DeviceToken)}\n" +
                $"Voice controls: {preferences.VoiceControlsEnabled}\n" +
                $"Global hotkey: {preferences.GlobalHotkeyEnabled}";
            await this.RefreshDeviceCapabilitiesAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            this.statusText.Text = "Startup refresh failed";
            this.detailText.Text = ex.Message;
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
        var preferences = await this.appState.Preferences.LoadAsync();
        var messages = await this.appState.Realtime.LoadChatHistoryAsync(preferences.ChatSessionKey);
        this.chatMessages.Children.Clear();
        foreach (var message in messages)
        {
            this.chatMessages.Children.Add(new TextBlock
            {
                Text = $"{message.Role}: {message.Text}",
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    private async Task SendChatAsync()
    {
        var message = this.chatInput.Text.Trim();
        if (message.Length == 0)
        {
            return;
        }
        var preferences = await this.appState.Preferences.LoadAsync();
        await this.appState.Realtime.SendChatAsync(preferences.ChatSessionKey, message);
        this.chatInput.Text = "";
        await this.RefreshChatAsync();
    }

    private async Task RefreshApprovalsAsync()
    {
        var approvals = await this.appState.Realtime.ListApprovalsAsync();
        this.approvalsList.Children.Clear();
        foreach (var approval in approvals)
        {
            this.approvalsList.Children.Add(this.BuildApprovalRow(approval));
        }
    }

    private UIElement BuildApprovalRow(PendingApproval approval)
    {
        var row = new StackPanel { Spacing = 6 };
        row.Children.Add(new TextBlock
        {
            Text = $"{approval.Id}: {approval.Command}",
            TextWrapping = TextWrapping.Wrap,
        });
        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(new XamlButton
        {
            Content = "Allow once",
            Command = this.CreateCommand(async () =>
            {
                await this.appState.Realtime.ResolveApprovalAsync(approval.Id, "allow-once");
                await this.RefreshApprovalsAsync();
            }),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Deny",
            Command = this.CreateCommand(async () =>
            {
                await this.appState.Realtime.ResolveApprovalAsync(approval.Id, "deny");
                await this.RefreshApprovalsAsync();
            }),
        });
        row.Children.Add(buttons);
        return row;
    }

    private async Task RefreshPairingAsync()
    {
        var requests = await this.appState.Realtime.ListPairingRequestsAsync();
        this.pairingList.Children.Clear();
        foreach (var request in requests)
        {
            this.pairingList.Children.Add(this.BuildPairingRow(request));
        }
    }

    private UIElement BuildPairingRow(PairingRequest request)
    {
        var row = new StackPanel { Spacing = 6 };
        row.Children.Add(new TextBlock
        {
            Text = $"{request.Kind}: {request.DisplayName} ({request.DeviceId})",
            TextWrapping = TextWrapping.Wrap,
        });
        var buttons = new StackPanel { Orientation = XamlOrientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(new XamlButton
        {
            Content = "Approve",
            Command = this.CreateCommand(async () =>
            {
                await this.appState.Realtime.ResolvePairingAsync(request, approve: true);
                await this.RefreshPairingAsync();
            }),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Reject",
            Command = this.CreateCommand(async () =>
            {
                await this.appState.Realtime.ResolvePairingAsync(request, approve: false);
                await this.RefreshPairingAsync();
            }),
        });
        row.Children.Add(buttons);
        return row;
    }

    private async Task SaveSettingsAsync()
    {
        await this.appState.Preferences.UpdateAsync(current => current with
        {
            GatewayUrl = string.IsNullOrWhiteSpace(this.gatewayUrlInput.Text)
                ? AppPreferences.Default.GatewayUrl
                : this.gatewayUrlInput.Text.Trim(),
            GatewayToken = string.IsNullOrWhiteSpace(this.gatewayTokenInput.Password) ? null : this.gatewayTokenInput.Password.Trim(),
            ChatSessionKey = string.IsNullOrWhiteSpace(this.chatSessionInput.Text)
                ? AppPreferences.Default.ChatSessionKey
                : this.chatSessionInput.Text.Trim(),
            VoiceControlsEnabled = this.voiceControlsToggle.IsChecked == true,
            GlobalHotkeyEnabled = this.globalHotkeyToggle.IsChecked == true,
        });
        await this.RefreshAllAsync();
    }

    private async Task RefreshDeviceCapabilitiesAsync()
    {
        var preferences = await this.appState.Preferences.LoadAsync();
        this.voiceControlsToggle.IsChecked = preferences.VoiceControlsEnabled;
        this.globalHotkeyToggle.IsChecked = preferences.GlobalHotkeyEnabled;
        this.SyncHotkeyRegistration(preferences.GlobalHotkeyEnabled);

        this.deviceStatusList.Children.Clear();
        foreach (var status in this.appState.DeviceCapabilities.GetPermissionStatus())
        {
            this.deviceStatusList.Children.Add(new TextBlock
            {
                Text = $"{status.Capability}: {status.State} - {status.Detail}",
                TextWrapping = TextWrapping.Wrap,
            });
        }

        this.mediaDevicesList.Children.Clear();
        try
        {
            foreach (var camera in await this.appState.DeviceCapabilities.ListCameraDevicesAsync())
            {
                this.mediaDevicesList.Children.Add(new TextBlock { Text = $"Camera: {camera.Name}", TextWrapping = TextWrapping.Wrap });
            }
            foreach (var microphone in await this.appState.DeviceCapabilities.ListMicrophoneDevicesAsync())
            {
                this.mediaDevicesList.Children.Add(new TextBlock { Text = $"Microphone: {microphone.Name}", TextWrapping = TextWrapping.Wrap });
            }
        }
        catch (Exception ex)
        {
            this.mediaDevicesList.Children.Add(new TextBlock
            {
                Text = $"Media device enumeration failed: {ex.Message}",
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    private async Task SaveDevicePreferencesAsync()
    {
        await this.appState.Preferences.UpdateAsync(current => current with
        {
            VoiceControlsEnabled = this.voiceControlsToggle.IsChecked == true,
            GlobalHotkeyEnabled = this.globalHotkeyToggle.IsChecked == true,
        });
        await this.RefreshDeviceCapabilitiesAsync();
    }

    private async Task CaptureScreenAsync()
    {
        this.nativeActionsText.Text = "Capturing screen...";
        var result = await Task.Run(this.appState.DeviceCapabilities.CapturePrimaryScreen);
        this.nativeActionsText.Text = result.Detail;
        if (result.Succeeded && !string.IsNullOrWhiteSpace(result.Path))
        {
            WindowsShell.OpenFileInExplorer(result.Path);
        }
    }

    private async Task CaptureScreenFramesAsync()
    {
        this.nativeActionsText.Text = "Recording screen frame sequence...";
        var captures = await Task.Run(() => this.appState.DeviceCapabilities.CaptureScreenFrameSequence());
        var successful = captures.Where(capture => capture.Succeeded).ToArray();
        this.nativeActionsText.Text = $"Captured {successful.Length} screen frames in {this.appState.DeviceCapabilities.CaptureRoot}";
        if (successful.Length > 0)
        {
            WindowsShell.OpenFileInExplorer(this.appState.DeviceCapabilities.CaptureRoot);
        }
    }

    private async Task CaptureCameraPhotoAsync()
    {
        this.nativeActionsText.Text = "Capturing camera photo...";
        var result = await this.appState.DeviceCapabilities.CaptureCameraPhotoAsync();
        this.nativeActionsText.Text = result.Detail;
        if (result.Succeeded && !string.IsNullOrWhiteSpace(result.Path))
        {
            WindowsShell.OpenFileInExplorer(result.Path);
        }
    }

    private void ShowNotification(string title, string message)
    {
        if (this.trayHost is null)
        {
            this.nativeActionsText.Text = "Tray host is not ready for notifications.";
            return;
        }

        this.trayHost.ShowNotification(title, message);
        this.nativeActionsText.Text = "Notification sent.";
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

        this.nativeActionsText.Text = "Overlay shown.";
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
                this.nativeActionsText.Text = "Push-to-talk hotkey pressed.";
                this.ShowOverlay("Push-to-talk", "Ctrl+Shift+Space was received by the Windows companion.");
            }));
        try
        {
            this.hotkeyService.RegisterPushToTalkHotkey();
        }
        catch (Exception ex)
        {
            this.nativeActionsText.Text = ex.Message;
        }
    }

    private async Task RunGatewayActionAsync(GatewayCliAction action)
    {
        this.statusText.Text = $"{action} in progress...";
        var result = await this.appState.Gateway.RunActionAsync(action);
        this.RenderStatus(result.Status);
        if (!result.Succeeded)
        {
            this.detailText.Text = result.Output;
        }
        await this.RefreshAllAsync();
    }

    private void RenderStatus(GatewayStatusSnapshot status)
    {
        this.logPath = status.LogPath;
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
    }
}
