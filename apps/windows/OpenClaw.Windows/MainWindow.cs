using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Windows.Native;
using XamlButton = Microsoft.UI.Xaml.Controls.Button;
using XamlHorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using XamlOrientation = Microsoft.UI.Xaml.Controls.Orientation;
using XamlTextBox = Microsoft.UI.Xaml.Controls.TextBox;

namespace OpenClaw.Windows;

public sealed class MainWindow : Window
{
    private readonly WindowsCompanionState appState;
    private readonly TextBlock statusText = new();
    private readonly TextBlock detailText = new();
    private readonly StackPanel onboardingList = new() { Spacing = 6 };
    private readonly StackPanel chatMessages = new() { Spacing = 8 };
    private readonly StackPanel approvalsList = new() { Spacing = 8 };
    private readonly StackPanel pairingList = new() { Spacing = 8 };
    private readonly TextBlock settingsText = new();
    private readonly XamlTextBox chatInput = new() { AcceptsReturn = true, Height = 88, TextWrapping = TextWrapping.Wrap };
    private readonly XamlTextBox gatewayUrlInput = new();
    private readonly XamlTextBox gatewayTokenInput = new();
    private readonly XamlTextBox chatSessionInput = new();
    private string? logPath;

    public MainWindow(WindowsCompanionState appState)
    {
        this.appState = appState;
        this.Title = "OpenClaw";
        this.appState.Realtime.StateChanged += (state, reason) =>
        {
            _ = this.DispatcherQueue.TryEnqueue(() =>
            {
                this.statusText.Text = $"Gateway: {state}";
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    this.detailText.Text = reason;
                }
            });
        };
        this.appState.Realtime.EventReceived += @event =>
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
        };
        this.Content = this.BuildContent();
    }

    public void ShowShell()
    {
        this.Activate();
    }

    public async void RunGatewayAction(GatewayCliAction action)
    {
        await this.RunGatewayActionAsync(action);
    }

    private UIElement BuildContent()
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
            Text = "OpenClaw",
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        header.Children.Add(new TextBlock
        {
            Text = $"Gateway protocol {this.appState.Summary.GatewayProtocolVersion}",
            Opacity = 0.72,
        });
        root.Children.Add(header);

        var tabs = new TabView
        {
            IsAddTabButtonVisible = false,
            Margin = new Thickness(0, 20, 0, 0),
        };
        Grid.SetRow(tabs, 1);
        tabs.TabItems.Add(new TabViewItem { Header = "Status", Content = this.BuildStatusPanel() });
        tabs.TabItems.Add(new TabViewItem { Header = "Chat", Content = this.BuildChatPanel() });
        tabs.TabItems.Add(new TabViewItem { Header = "Approvals", Content = this.BuildApprovalsPanel() });
        tabs.TabItems.Add(new TabViewItem { Header = "Pairing", Content = this.BuildPairingPanel() });
        tabs.TabItems.Add(new TabViewItem { Header = "Settings", Content = this.BuildSettingsPanel() });
        root.Children.Add(tabs);
        _ = this.RefreshAllAsync();
        return root;
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
            Command = new RelayCommand(async () => await this.ConnectRealtimeAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Open Logs",
            Command = new RelayCommand(() =>
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
            Command = new RelayCommand(async () => await this.RefreshChatAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Send",
            Command = new RelayCommand(async () => await this.SendChatAsync()),
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
            Command = new RelayCommand(async () => await this.RefreshApprovalsAsync()),
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
            Command = new RelayCommand(async () => await this.RefreshPairingAsync()),
        });
        panel.Children.Add(this.pairingList);
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
            Command = new RelayCommand(async () => await this.SaveSettingsAsync()),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Refresh",
            Command = new RelayCommand(async () => await this.RefreshAllAsync()),
        });
        panel.Children.Add(buttons);
        return panel;
    }

    private XamlButton ActionButton(string label, GatewayCliAction action)
    {
        return new XamlButton
        {
            Content = label,
            Command = new RelayCommand(async () => await this.RunGatewayActionAsync(action)),
        };
    }

    private async Task RefreshAllAsync()
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
        this.gatewayTokenInput.Text = preferences.GatewayToken ?? "";
        this.chatSessionInput.Text = preferences.ChatSessionKey;
        this.settingsText.Text =
            $"Open main window on launch: {preferences.OpenMainWindowOnLaunch}\n" +
            $"Last status: {preferences.LastStatus ?? "unknown"}\n" +
            $"Last checked: {preferences.LastStatusCheckedAt?.ToLocalTime().ToString("g") ?? "never"}\n" +
            $"Device token cached: {!string.IsNullOrWhiteSpace(preferences.DeviceToken)}";
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
            Command = new RelayCommand(async () =>
            {
                await this.appState.Realtime.ResolveApprovalAsync(approval.Id, "allow-once");
                await this.RefreshApprovalsAsync();
            }),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Deny",
            Command = new RelayCommand(async () =>
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
            Command = new RelayCommand(async () =>
            {
                await this.appState.Realtime.ResolvePairingAsync(request, approve: true);
                await this.RefreshPairingAsync();
            }),
        });
        buttons.Children.Add(new XamlButton
        {
            Content = "Reject",
            Command = new RelayCommand(async () =>
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
            GatewayToken = string.IsNullOrWhiteSpace(this.gatewayTokenInput.Text) ? null : this.gatewayTokenInput.Text.Trim(),
            ChatSessionKey = string.IsNullOrWhiteSpace(this.chatSessionInput.Text)
                ? AppPreferences.Default.ChatSessionKey
                : this.chatSessionInput.Text.Trim(),
        });
        await this.RefreshAllAsync();
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
        this.detailText.Text =
            $"Service installed: {status.ServiceInstalled}\n" +
            $"Reachable: {status.Reachable}\n" +
            $"Capability: {status.Capability}\n" +
            $"Dashboard: {status.DashboardUrl ?? "unknown"}\n" +
            $"Logs: {status.LogPath ?? "unknown"}";
    }
}
