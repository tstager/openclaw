using OpenClaw.Windows.Native;
using XamlApplication = Microsoft.UI.Xaml.Application;
using XamlLaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace OpenClaw.Windows;

public sealed partial class App : XamlApplication
{
    private WindowsTrayHost? trayHost;
    private MainWindow? window;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += (_, args) =>
        {
            CrashLog.Write(args.Exception);
        };
    }

    protected override void OnLaunched(XamlLaunchActivatedEventArgs args)
    {
        try
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
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            throw;
        }
    }
}

internal static class CrashLog
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenClaw",
        "WindowsCompanion",
        "crash.log");

    public static void Write(Exception exception)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(
            Path,
            $"[{DateTimeOffset.Now:O}] {exception}\n\n");
    }
}
