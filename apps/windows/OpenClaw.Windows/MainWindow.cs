using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Windows.Native;
using Windows.UI;
using XamlButton = Microsoft.UI.Xaml.Controls.Button;
using XamlHorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using XamlOrientation = Microsoft.UI.Xaml.Controls.Orientation;

namespace OpenClaw.Windows;

public sealed class MainWindow : Window
{
    private readonly WindowsCompanionState appState;
    private readonly TextBlock statusText = new();
    private readonly TextBlock detailText = new();
    private readonly StackPanel onboardingList = new() { Spacing = 6 };
    private readonly TextBlock settingsText = new();
    private string? logPath;

    public MainWindow(WindowsCompanionState appState)
    {
        this.appState = appState;
        this.Title = "OpenClaw";
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

        var buttons = new StackPanel
        {
            Orientation = XamlOrientation.Horizontal,
            Spacing = 8,
        };
        buttons.Children.Add(ActionButton("Install", GatewayCliAction.Install));
        buttons.Children.Add(ActionButton("Start", GatewayCliAction.Start));
        buttons.Children.Add(ActionButton("Restart", GatewayCliAction.Restart));
        buttons.Children.Add(ActionButton("Stop", GatewayCliAction.Stop));
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

    private UIElement BuildSettingsPanel()
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Settings",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        panel.Children.Add(this.settingsText);
        panel.Children.Add(new XamlButton
        {
            Content = "Refresh",
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            Command = new RelayCommand(async () => await this.RefreshAllAsync()),
        });
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
        this.settingsText.Text =
            $"Open main window on launch: {preferences.OpenMainWindowOnLaunch}\n" +
            $"Last status: {preferences.LastStatus ?? "unknown"}\n" +
            $"Last checked: {preferences.LastStatusCheckedAt?.ToLocalTime().ToString("g") ?? "never"}";
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
