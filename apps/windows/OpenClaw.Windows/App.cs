using OpenClaw.Windows.Native;
using XamlApplication = Microsoft.UI.Xaml.Application;
using XamlLaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace OpenClaw.Windows;

public sealed partial class App : XamlApplication
{
    private const string SingleInstanceMutexName = "OpenClaw.Windows.Companion";

    private WindowsTrayHost? trayHost;
    private MainWindow? window;
    private System.Threading.Mutex? singleInstanceMutex;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += (_, args) =>
        {
            CrashLog.Write(args.Exception);
        };
    }

    protected override async void OnLaunched(XamlLaunchActivatedEventArgs args)
    {
        try
        {
            this.singleInstanceMutex = new System.Threading.Mutex(true, SingleInstanceMutexName, out var createdNew);
            if (!createdNew)
            {
                this.singleInstanceMutex.Dispose();
                this.singleInstanceMutex = null;
                Exit();
                return;
            }

            var appState = AppBootstrap.CreateAppState();
            var preferences = await appState.Preferences.LoadAsync();
            this.window = new MainWindow(appState);
            this.trayHost = new WindowsTrayHost(
                getGatewayStatus: () => this.window.GatewayStatusText,
                onShow: () => this.window.DispatcherQueue.TryEnqueue(this.window.ShowShell),
                onShowHome: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.ShowDestination("home")),
                onShowLogs: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.ShowDestination("logs")),
                onShowSettings: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.ShowDestination("settings")),
                onInstallGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Install)),
                onStartGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Start)),
                onRestartGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Restart)),
                onStopGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Stop)),
                onConnect: () => this.window.DispatcherQueue.TryEnqueue(this.window.ConnectGateway),
                onOpenLogs: () => this.window.DispatcherQueue.TryEnqueue(this.window.OpenLogs),
                onExit: () =>
                {
                    this.window.DispatcherQueue.TryEnqueue(() =>
                    {
                        this.window.ExitApplication();
                        this.trayHost?.Dispose();
                        this.trayHost = null;
                        this.ReleaseSingleInstanceMutex();
                        Exit();
                    });
                });
            this.window.AttachTrayHost(this.trayHost);

            this.window.Closed += (_, _) =>
            {
                this.trayHost?.Dispose();
                this.trayHost = null;
            };
            if (preferences.OpenMainWindowOnLaunch)
            {
                this.window.ShowShell();
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            this.ReleaseSingleInstanceMutex();
            throw;
        }
    }

    private void ReleaseSingleInstanceMutex()
    {
        if (this.singleInstanceMutex is null)
        {
            return;
        }

        this.singleInstanceMutex.ReleaseMutex();
        this.singleInstanceMutex.Dispose();
        this.singleInstanceMutex = null;
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
