using System.Collections.Concurrent;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenClaw.Protocol;
using OpenClaw.Protocol.Generated;

namespace OpenClaw.Windows;

public enum WindowsCanvasNodeState
{
    Disconnected,
    Connecting,
    Connected,
    PairingRequired,
    AuthFailed,
}

public sealed class WindowsCanvasNodeClient : IAsyncDisposable
{
    // The current stable gateway still rejects openclaw-windows as a node client id.
    private const string ClientId = "openclaw-macos";
    private const string ClientMode = "node";
    private const string ClientRole = "node";
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DisconnectCloseTimeout = TimeSpan.FromSeconds(2);
    private readonly AppPreferencesStore preferences;
    private readonly DeviceIdentityStore deviceIdentityStore;
    private readonly TimeSpan requestTimeout;
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> pending = new();
    private readonly JsonSerializerOptions jsonOptions = GatewayProtocolJson.SerializerOptions;
    private ClientWebSocket? socket;
    private CancellationTokenSource? receiveCts;
    private Task? receiveTask;
    private TaskCompletionSource<string>? connectChallenge;
    private int nextRequestId;

    public WindowsCanvasNodeClient(
        AppPreferencesStore preferences,
        DeviceIdentityStore deviceIdentityStore,
        TimeSpan? requestTimeout = null)
    {
        this.preferences = preferences;
        this.deviceIdentityStore = deviceIdentityStore;
        this.requestTimeout = requestTimeout ?? DefaultRequestTimeout;
    }

    public WindowsCanvasNodeState State { get; private set; } = WindowsCanvasNodeState.Disconnected;

    public string? CanvasSurfaceUrl { get; private set; }

    public string? A2UIHostUrl => WindowsCanvasA2UIUrl.ResolveFromCanvasPluginSurfaceUrl(this.CanvasSurfaceUrl);

    public Func<WindowsCanvasInvokeRequest, CancellationToken, Task<WindowsCanvasInvokeResponse>>? InvokeAsync { get; set; }

    public event Action<WindowsCanvasNodeState, string?>? StateChanged;

    public event Action<string?>? CanvasSurfaceUrlChanged;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var prefs = await this.preferences.LoadAsync(cancellationToken);
        await this.ConnectAsync(prefs, cancellationToken);
    }

    public async Task ConnectAsync(AppPreferences prefs, CancellationToken cancellationToken = default)
    {
        await this.DisconnectAsync();
        if (!prefs.CanvasNodeEnabled)
        {
            this.SetState(WindowsCanvasNodeState.Disconnected, "Canvas node is disabled in Settings.");
            return;
        }

        this.SetState(WindowsCanvasNodeState.Connecting, null);
        var connectingSocket = new ClientWebSocket();
        try
        {
            this.socket = connectingSocket;
            await connectingSocket.ConnectAsync(new Uri(prefs.GatewayUrl), cancellationToken);
            this.receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.connectChallenge = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.receiveTask = this.ReceiveLoopAsync(connectingSocket, this.receiveCts.Token);
            await this.SendConnectAsync(prefs, cancellationToken);
            this.connectChallenge = null;
            this.SetState(WindowsCanvasNodeState.Connected, null);
        }
        catch
        {
            await this.DisconnectAsync();
            throw;
        }
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        await this.ConnectAsync(cancellationToken);
    }

    public async Task<string?> RefreshCanvasSurfaceUrlAsync(CancellationToken cancellationToken = default)
    {
        var payload = await this.RequestAsync(
            "node.pluginSurface.refresh",
            new { surface = "canvas" },
            cancellationToken);
        if (payload.TryGetProperty("pluginSurfaceUrls", out var urls) &&
            urls.ValueKind == JsonValueKind.Object &&
            urls.TryGetProperty("canvas", out var canvasUrl) &&
            canvasUrl.ValueKind == JsonValueKind.String)
        {
            this.SetCanvasSurfaceUrl(canvasUrl.GetString());
        }
        return this.CanvasSurfaceUrl;
    }

    public async Task DisconnectAsync()
    {
        var socketToClose = this.socket;
        var receiveTaskSnapshot = this.receiveTask;
        var receiveCtsSnapshot = this.receiveCts;
        receiveCtsSnapshot?.Cancel();
        this.receiveCts = null;
        this.receiveTask = null;
        this.connectChallenge?.TrySetCanceled();
        this.connectChallenge = null;
        this.socket = null;
        this.SetCanvasSurfaceUrl(null);

        if (socketToClose is { State: WebSocketState.Open or WebSocketState.CloseReceived })
        {
            using var closeCts = new CancellationTokenSource(DisconnectCloseTimeout);
            try
            {
                await socketToClose.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", closeCts.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
            {
                socketToClose.Abort();
            }
        }

        socketToClose?.Dispose();
        receiveCtsSnapshot?.Dispose();
        if (receiveTaskSnapshot is not null)
        {
            try
            {
                await receiveTaskSnapshot.WaitAsync(DisconnectCloseTimeout);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or WebSocketException)
            {
            }
        }
        this.FailPending(new OperationCanceledException("Gateway node WebSocket disconnected."));
        this.SetState(WindowsCanvasNodeState.Disconnected, null);
    }

    public async ValueTask DisposeAsync()
    {
        await this.DisconnectAsync();
        this.sendGate.Dispose();
    }

    private async Task SendConnectAsync(AppPreferences prefs, CancellationToken cancellationToken)
    {
        var sharedToken = NormalizeToken(prefs.GatewayToken);
        var auth = new Dictionary<string, string>();
        if (sharedToken is not null)
        {
            auth["token"] = sharedToken;
        }

        var identity = await this.deviceIdentityStore.LoadOrCreateAsync(cancellationToken);
        var nonce = await this.WaitForConnectChallengeAsync(cancellationToken);
        var signedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var signedPayload = DeviceIdentityStore.BuildDeviceAuthPayloadV3(
            identity.DeviceId,
            ClientId,
            ClientMode,
            ClientRole,
            [],
            signedAt,
            sharedToken,
            nonce,
            "windows",
            deviceFamily: null);

        var connectParams = new Dictionary<string, object?>
        {
            ["minProtocol"] = GatewayProtocol.Version,
            ["maxProtocol"] = GatewayProtocol.Version,
            ["client"] = new
            {
                id = ClientId,
                version = "0.1.0",
                platform = "windows",
                mode = ClientMode,
            },
            ["role"] = ClientRole,
            ["scopes"] = Array.Empty<string>(),
            ["caps"] = new[] { "canvas" },
            ["commands"] = WindowsCanvasCommands.All,
            ["permissions"] = new { },
            ["locale"] = Thread.CurrentThread.CurrentCulture.Name,
            ["userAgent"] = "openclaw-windows/0.1.0",
            ["device"] = new
            {
                id = identity.DeviceId,
                publicKey = identity.PublicKeyPem,
                signature = identity.SignPayload(signedPayload),
                signedAt,
                nonce,
            },
        };
        if (auth.Count > 0)
        {
            connectParams["auth"] = auth;
        }

        var responsePayload = await this.RequestAsync("connect", connectParams, cancellationToken);
        this.ReadPluginSurfaceUrls(responsePayload);
    }

    private async Task<JsonElement> RequestAsync(
        string method,
        object parameters,
        CancellationToken cancellationToken)
    {
        var socketSnapshot = this.socket;
        if (socketSnapshot is not { State: WebSocketState.Open })
        {
            throw new InvalidOperationException("Gateway node WebSocket is not connected.");
        }

        var id = Interlocked.Increment(ref this.nextRequestId).ToString(CultureInfo.InvariantCulture);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.pending[id] = completion;
        using var timeoutCts = new CancellationTokenSource(this.requestTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var registration = linkedCts.Token.Register(() =>
        {
            if (!this.pending.TryRemove(id, out var pendingCompletion))
            {
                return;
            }

            pendingCompletion.TrySetException(timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                ? new TimeoutException($"Gateway request '{method}' timed out after {this.requestTimeout.TotalSeconds:0.#} seconds.")
                : new OperationCanceledException(cancellationToken));
        });

        try
        {
            await this.SendJsonAsync(socketSnapshot, new
            {
                type = "req",
                id,
                method,
                @params = parameters,
            }, linkedCts.Token);
            return await completion.Task;
        }
        catch
        {
            this.pending.TryRemove(id, out _);
            throw;
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socketSnapshot, CancellationToken cancellationToken)
    {
        Exception? disconnectReason = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested && socketSnapshot.State == WebSocketState.Open)
            {
                var json = await ReceiveTextAsync(socketSnapshot, cancellationToken);
                if (json is null)
                {
                    disconnectReason = new IOException("Gateway node WebSocket closed.");
                    break;
                }
                this.HandleFrame(json, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            disconnectReason = ex;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                disconnectReason ??= new IOException("Gateway node WebSocket disconnected.");
                this.FailPending(disconnectReason);
                if (ReferenceEquals(this.socket, socketSnapshot))
                {
                    this.socket = null;
                    this.SetState(WindowsCanvasNodeState.Disconnected, disconnectReason.Message);
                }
            }
        }
    }

    private void HandleFrame(string json, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(json);
        var frame = document.RootElement;
        var type = ReadString(frame, "type");
        if (string.Equals(type, "res", StringComparison.Ordinal))
        {
            this.HandleResponse(frame);
            return;
        }
        if (!string.Equals(type, "event", StringComparison.Ordinal))
        {
            return;
        }

        var eventName = ReadString(frame, "event");
        if (string.Equals(eventName, "connect.challenge", StringComparison.Ordinal))
        {
            if (frame.TryGetProperty("payload", out var challengePayload))
            {
                this.HandleConnectChallenge(challengePayload);
            }
            return;
        }
        if (string.Equals(eventName, "node.invoke.request", StringComparison.Ordinal) &&
            frame.TryGetProperty("payload", out var invokePayload))
        {
            _ = Task.Run(
                async () => await this.HandleInvokeRequestAsync(invokePayload.Clone(), cancellationToken),
                CancellationToken.None);
        }
    }

    private void HandleConnectChallenge(JsonElement payload)
    {
        if (payload.TryGetProperty("nonce", out var nonce) &&
            nonce.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(nonce.GetString()))
        {
            this.connectChallenge?.TrySetResult(nonce.GetString()!);
        }
    }

    private async Task HandleInvokeRequestAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = new WindowsCanvasInvokeRequest(
            ReadString(payload, "id") ?? "",
            ReadString(payload, "command") ?? "",
            ReadString(payload, "paramsJSON"),
            ReadString(payload, "nodeId"));
        var handler = this.InvokeAsync;
        var response = handler is null
            ? WindowsCanvasInvokeResponse.Failure("UNAVAILABLE", "Windows Canvas handler is not ready.")
            : await handler(request, cancellationToken);

        await this.RequestAsync(
            "node.invoke.result",
            new
            {
                id = request.Id,
                nodeId = request.NodeId,
                ok = response.Ok,
                payloadJSON = response.PayloadJson,
                error = response.Error is null
                    ? null
                    : new { code = response.Error.Code, message = response.Error.Message },
            },
            cancellationToken);
    }

    private void HandleResponse(JsonElement response)
    {
        var id = ReadString(response, "id") ?? "";
        if (!this.pending.TryRemove(id, out var completion))
        {
            return;
        }

        if (response.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True)
        {
            if (response.TryGetProperty("payload", out var payload))
            {
                completion.TrySetResult(payload.Clone());
                return;
            }
            using var emptyPayload = JsonDocument.Parse("{}");
            completion.TrySetResult(emptyPayload.RootElement.Clone());
            return;
        }

        var error = response.TryGetProperty("error", out var errorPayload) ? errorPayload : default;
        var code = ReadString(error, "code") ?? "UNKNOWN";
        var message = ReadString(error, "message") ?? "Gateway request failed.";
        if (code.Contains("PAIR", StringComparison.OrdinalIgnoreCase))
        {
            this.SetState(WindowsCanvasNodeState.PairingRequired, message);
        }
        else if (code.Contains("AUTH", StringComparison.OrdinalIgnoreCase) ||
                 code.Contains("TOKEN", StringComparison.OrdinalIgnoreCase))
        {
            this.SetState(WindowsCanvasNodeState.AuthFailed, message);
        }
        completion.TrySetException(new GatewayRpcException(code, message));
    }

    private void ReadPluginSurfaceUrls(JsonElement payload)
    {
        if (payload.TryGetProperty("pluginSurfaceUrls", out var urls) &&
            urls.ValueKind == JsonValueKind.Object &&
            urls.TryGetProperty("canvas", out var canvasUrl) &&
            canvasUrl.ValueKind == JsonValueKind.String)
        {
            this.SetCanvasSurfaceUrl(canvasUrl.GetString());
        }
    }

    private async Task SendJsonAsync(
        ClientWebSocket socketSnapshot,
        object payload,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, this.jsonOptions));
        await this.sendGate.WaitAsync(cancellationToken);
        try
        {
            await socketSnapshot.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            this.sendGate.Release();
        }
    }

    private async Task<string> WaitForConnectChallengeAsync(CancellationToken cancellationToken)
    {
        var challenge = this.connectChallenge ??
            throw new InvalidOperationException("Gateway connect challenge is not initialized.");
        using var timeout = new CancellationTokenSource(this.requestTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var registration = linked.Token.Register(() =>
        {
            if (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                challenge.TrySetException(new TimeoutException("Gateway connect challenge timed out."));
                return;
            }
            challenge.TrySetCanceled(cancellationToken.IsCancellationRequested ? cancellationToken : linked.Token);
        });
        return await challenge.Task;
    }

    private void SetState(WindowsCanvasNodeState state, string? reason)
    {
        this.State = state;
        this.StateChanged?.Invoke(state, reason);
    }

    private void SetCanvasSurfaceUrl(string? canvasSurfaceUrl)
    {
        if (string.Equals(this.CanvasSurfaceUrl, canvasSurfaceUrl, StringComparison.Ordinal))
        {
            return;
        }
        this.CanvasSurfaceUrl = canvasSurfaceUrl;
        this.CanvasSurfaceUrlChanged?.Invoke(this.A2UIHostUrl);
    }

    private void FailPending(Exception exception)
    {
        foreach (var entry in this.pending.ToArray())
        {
            if (this.pending.TryRemove(entry.Key, out var completion))
            {
                completion.TrySetException(exception);
            }
        }
    }

    private static async Task<string?> ReceiveTextAsync(
        ClientWebSocket socketSnapshot,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socketSnapshot.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string? NormalizeToken(string? token)
    {
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
