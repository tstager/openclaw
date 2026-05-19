using OpenClaw.Windows.Native;
using XamlApplication = Microsoft.UI.Xaml.Application;
using XamlLaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace OpenClaw.Windows;

/// <summary>
/// WinUI application entry point that owns single-instance lifetime, tray wiring, and crash logging.
/// </summary>
public sealed partial class App : XamlApplication, IDisposable
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

    /// <summary>
    /// Creates the app service graph, main window, and tray host after enforcing one running instance.
    /// </summary>
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
            this.window.ApplyThemePreference(preferences.ThemePreference);
            // Tray callbacks run outside WinUI's normal input flow, so marshal every UI action to the window dispatcher.
            this.trayHost = new WindowsTrayHost(
                getGatewayStatus: () => this.window.GatewayStatusText,
                getLatestActivity: () => this.window.LatestActivityText,
                onShow: () => this.window.DispatcherQueue.TryEnqueue(this.window.ShowShell),
                onShowHome: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.ShowDestination(WindowsNavigationDestination.Home)),
                onShowLogs: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.ShowDestination(WindowsNavigationDestination.Logs)),
                onShowSettings: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.ShowDestination(WindowsNavigationDestination.Settings)),
                onInstallGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Install)),
                onStartGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Start)),
                onRestartGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Restart)),
                onStopGateway: () => this.window.DispatcherQueue.TryEnqueue(() => this.window.RunGatewayAction(GatewayCliAction.Stop)),
                onConnect: () => this.window.DispatcherQueue.TryEnqueue(this.window.ConnectGateway),
                onOpenLogs: () => this.window.DispatcherQueue.TryEnqueue(this.window.OpenLogs),
                onNotificationClicked: () => this.window.DispatcherQueue.TryEnqueue(this.window.ShowLatestNotificationDestination),
                onExit: () =>
                {
                    this.window.DispatcherQueue.TryEnqueue(async () =>
                    {
                        try
                        {
                            await this.window.ExitApplicationAsync();
                        }
                        catch (Exception ex)
                        {
                            CrashLog.Write(ex);
                        }
                        finally
                        {
                            this.trayHost?.Dispose();
                            this.trayHost = null;
                            this.ReleaseSingleInstanceMutex();
                            Exit();
                        }
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

    /// <summary>
    /// Releases native process-wide resources when the app exits or launch fails.
    /// </summary>
    public void Dispose()
    {
        this.trayHost?.Dispose();
        this.trayHost = null;
        this.ReleaseSingleInstanceMutex();
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

/// <summary>
/// App-local append-only crash log used when UI startup or event handlers fail before diagnostics are visible.
/// </summary>
internal static class CrashLog
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenClaw",
        "WindowsCompanion",
        "crash.log");

    /// <summary>
    /// Records an exception without rethrowing so callers can decide whether to continue or exit.
    /// </summary>
    public static void Write(Exception exception)
    {
        WriteEntry(exception.ToString());
    }

    public static void WriteMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        WriteEntry(message.Trim());
    }

    private static void WriteEntry(string message)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(
            Path,
            $"[{DateTimeOffset.Now:O}] {message}\n\n");
    }
}
