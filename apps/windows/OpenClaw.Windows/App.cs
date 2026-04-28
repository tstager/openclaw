using OpenClaw.Windows.Native;
using XamlApplication = Microsoft.UI.Xaml.Application;
using XamlLaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace OpenClaw.Windows;

public sealed class App : XamlApplication
{
    private WindowsTrayHost? trayHost;
    private MainWindow? window;

    protected override void OnLaunched(XamlLaunchActivatedEventArgs args)
    {
        var appState = AppBootstrap.CreateAppState();
        this.window = new MainWindow(appState);
        this.trayHost = new WindowsTrayHost(
            onShow: () => this.window.DispatcherQueue.TryEnqueue(this.window.ShowShell),
            onInstallGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Install)),
            onStartGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Start)),
            onRestartGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Restart)),
            onStopGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Stop)),
            onExit: () =>
            {
                this.trayHost?.Dispose();
                Exit();
            });
        this.window.AttachTrayHost(this.trayHost);

        this.window.Closed += (_, _) => this.trayHost?.Dispose();
        this.window.Activate();
    }
}
