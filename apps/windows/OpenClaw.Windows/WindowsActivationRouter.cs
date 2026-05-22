using System.IO.Pipes;

namespace OpenClaw.Windows;

/// <summary>
/// Parsed app-local activation request delivered from command-line or protocol launches.
/// </summary>
public sealed record WindowsActivationRequest(
    string Destination,
    string? ChatSessionKey,
    string? SourceUri);

/// <summary>
/// Parses app-local deep-link payloads into shell navigation requests.
/// </summary>
public static class WindowsActivationRouter
{
    public static WindowsActivationRequest? ParseLaunchArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return null;
        }

        var trimmed = arguments.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "openclaw", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ParseUri(uri);
    }

    public static WindowsActivationRequest ParseUri(Uri uri)
    {
        var segment = !string.IsNullOrWhiteSpace(uri.Host)
            ? uri.Host
            : uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var destination = segment.ToLowerInvariant() switch
        {
            "chat" => WindowsNavigationDestination.Chat,
            "canvas" => WindowsNavigationDestination.Canvas,
            "sessions" => WindowsNavigationDestination.Sessions,
            "approvals" => WindowsNavigationDestination.Approvals,
            "pairing" => WindowsNavigationDestination.Pairing,
            "devices" => WindowsNavigationDestination.Devices,
            "logs" or "diagnostics" => WindowsNavigationDestination.Logs,
            "settings" => WindowsNavigationDestination.Settings,
            _ => WindowsNavigationDestination.Home,
        };
        var sessionKey = ReadQuery(uri, "session");
        if (destination == WindowsNavigationDestination.Home && !string.IsNullOrWhiteSpace(sessionKey))
        {
            destination = WindowsNavigationDestination.Chat;
        }

        return new WindowsActivationRequest(destination, sessionKey, uri.AbsoluteUri);
    }

    private static string? ReadQuery(Uri uri, string key)
    {
        var query = uri.Query.TrimStart('?');
        if (query.Length == 0)
        {
            return null;
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (!string.Equals(parts[0], key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return parts.Length == 2
                ? Uri.UnescapeDataString(parts[1])
                : "";
        }

        return null;
    }
}

/// <summary>
/// Forwards deep-link activation payloads from secondary launches to the primary app instance.
/// </summary>
public sealed class WindowsActivationRelay(string pipeName) : IDisposable
{
    private readonly string pipeName = pipeName;
    private CancellationTokenSource? cancellation;
    private Task? listenTask;

    public event Func<WindowsActivationRequest, Task>? RequestReceived;

    public void Start()
    {
        if (this.listenTask is not null)
        {
            return;
        }

        this.cancellation = new CancellationTokenSource();
        this.listenTask = this.ListenLoopAsync(this.cancellation.Token);
    }

    public async Task<bool> ForwardAsync(string launchArguments, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(launchArguments))
        {
            return false;
        }

        try
        {
            using var client = new NamedPipeClientStream(".", this.pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(timeout.Token);
            await using var writer = new StreamWriter(client) { AutoFlush = true };
            await writer.WriteAsync(launchArguments.Trim());
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or TimeoutException or OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        this.cancellation?.Cancel();
        this.cancellation?.Dispose();
        this.cancellation = null;
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    this.pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(server);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var request = WindowsActivationRouter.ParseLaunchArguments(payload);
                if (request is null || this.RequestReceived is null)
                {
                    continue;
                }

                await this.RequestReceived.Invoke(request);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }
        }
    }
}
