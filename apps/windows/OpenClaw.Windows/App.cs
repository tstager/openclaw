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
    private const string ActivationPipeName = "OpenClaw.Windows.Companion.Activation";

    private WindowsTrayHost? trayHost;
    private WindowsCompanionState? appState;
    private MainWindow? window;
    private System.Threading.Mutex? singleInstanceMutex;
    private WindowsActivationRequest? pendingActivationRequest;

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
            var activationRequest = WindowsActivationRouter.ParseLaunchArguments(args.Arguments);
            this.singleInstanceMutex = new System.Threading.Mutex(true, SingleInstanceMutexName, out var createdNew);
            if (!createdNew)
            {
                if (activationRequest is not null)
                {
                    using var relay = new WindowsActivationRelay(ActivationPipeName);
                    await relay.ForwardAsync(args.Arguments);
                }
                this.singleInstanceMutex.Dispose();
                this.singleInstanceMutex = null;
                Exit();
                return;
            }

            this.appState = AppBootstrap.CreateAppState();
            var preferences = await this.appState.Preferences.LoadAsync();
            this.window = new MainWindow(this.appState);
            this.appState.Activation.RequestReceived += this.OnActivationRequestReceivedAsync;
            this.appState.Activation.Start();
            this.window.ApplyAppearancePreferences(
                preferences.ThemePreference,
                preferences.AccentColorPreference,
                preferences.CustomAccentColor,
                preferences.ColorThemePreference,
                preferences.CustomColorTheme);
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
                this.appState?.Activation.Dispose();
            };
            if (preferences.OpenMainWindowOnLaunch || activationRequest is not null)
            {
                this.window.ShowShell();
            }

            if (activationRequest is not null)
            {
                await this.ActivateWindowRequestAsync(activationRequest);
            }
            else if (this.pendingActivationRequest is not null)
            {
                var pending = this.pendingActivationRequest;
                this.pendingActivationRequest = null;
                await this.ActivateWindowRequestAsync(pending);
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
        this.appState?.Activation.Dispose();
        this.appState = null;
        this.ReleaseSingleInstanceMutex();
    }

    private Task OnActivationRequestReceivedAsync(WindowsActivationRequest request)
    {
        if (this.window is null)
        {
            this.pendingActivationRequest = request;
            return Task.CompletedTask;
        }

        return this.ActivateWindowRequestAsync(request);
    }

    private Task ActivateWindowRequestAsync(WindowsActivationRequest request)
    {
        if (this.window is null)
        {
            this.pendingActivationRequest = request;
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!this.window.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await this.window.HandleActivationAsync(request);
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }))
        {
            completion.SetException(new InvalidOperationException("Unable to dispatch activation to the main window."));
        }

        return completion.Task;
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
