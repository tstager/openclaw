using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenClaw.Protocol;
using OpenClaw.Protocol.Generated;

namespace OpenClaw.Windows;

public enum GatewayRealtimeState
{
    Disconnected,
    Connecting,
    Connected,
    PairingRequired,
    AuthFailed,
}

public sealed record GatewayRealtimeEvent(string Name, JsonElement? Payload);

public sealed record ChatMessage(string Role, string Text);

public sealed record PendingApproval(
    string Id,
    string Command,
    string? Cwd,
    string? AgentId,
    string? SessionKey);

public sealed record PairingRequest(
    string RequestId,
    string Kind,
    string DisplayName,
    string DeviceId);

public sealed class GatewayRpcException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class GatewayRealtimeClient : IAsyncDisposable
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DisconnectCloseTimeout = TimeSpan.FromSeconds(2);
    private readonly TimeSpan requestTimeout;
    private readonly AppPreferencesStore preferences;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> pending = new();
    private readonly JsonSerializerOptions jsonOptions = GatewayProtocolJson.SerializerOptions;
    private ClientWebSocket? socket;
    private CancellationTokenSource? receiveCts;
    private Task? receiveTask;
    private int nextRequestId;

    public GatewayRealtimeClient(AppPreferencesStore preferences, TimeSpan? requestTimeout = null)
    {
        this.preferences = preferences;
        this.requestTimeout = requestTimeout ?? DefaultRequestTimeout;
    }

    public GatewayRealtimeState State { get; private set; } = GatewayRealtimeState.Disconnected;

    public event Action<GatewayRealtimeEvent>? EventReceived;

    public event Action<GatewayRealtimeState, string?>? StateChanged;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var prefs = await this.preferences.LoadAsync(cancellationToken);
        await this.ConnectAsync(prefs, cancellationToken);
    }

    public async Task ConnectAsync(AppPreferences prefs, CancellationToken cancellationToken = default)
    {
        await this.DisconnectAsync();
        this.SetState(GatewayRealtimeState.Connecting, null);
        var connectingSocket = new ClientWebSocket();
        try
        {
            this.socket = connectingSocket;
            await connectingSocket.ConnectAsync(new Uri(prefs.GatewayUrl), cancellationToken);
            this.receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.receiveTask = this.ReceiveLoopAsync(connectingSocket, this.receiveCts.Token);
            await this.SendConnectAsync(prefs, cancellationToken);
            this.SetState(GatewayRealtimeState.Connected, null);
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

    public async Task DisconnectAsync()
    {
        var socketToClose = this.socket;
        var receiveTaskSnapshot = this.receiveTask;
        var receiveCtsSnapshot = this.receiveCts;
        receiveCtsSnapshot?.Cancel();
        this.receiveCts = null;
        this.receiveTask = null;
        this.socket = null;

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
        this.FailPending(new OperationCanceledException("Gateway WebSocket disconnected."));
        this.SetState(GatewayRealtimeState.Disconnected, null);
    }

    public async ValueTask DisposeAsync()
    {
        await this.DisconnectAsync();
    }

    public async Task<IReadOnlyList<ChatMessage>> LoadChatHistoryAsync(
        string sessionKey,
        CancellationToken cancellationToken = default)
    {
        var payload = await this.RequestAsync("chat.history", new { sessionKey, limit = 80 }, cancellationToken);
        return ParseChatHistoryPayload(payload);
    }

    public async Task SendChatAsync(
        string sessionKey,
        string message,
        CancellationToken cancellationToken = default)
    {
        await this.RequestAsync(
            "chat.send",
            new
            {
                sessionKey,
                message,
                idempotencyKey = $"windows-{Guid.NewGuid():N}",
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<PendingApproval>> ListApprovalsAsync(CancellationToken cancellationToken = default)
    {
        var payload = await this.RequestAsync("exec.approval.list", new { }, cancellationToken);
        return ParseApprovalsPayload(payload);
    }

    public static IReadOnlyList<ChatMessage> ParseChatHistoryPayload(JsonElement payload)
    {
        if (!payload.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return messages.EnumerateArray()
            .Select(ParseChatMessage)
            .Where(static message => message is not null)
            .Cast<ChatMessage>()
            .ToArray();
    }

    public static IReadOnlyList<PendingApproval> ParseApprovalsPayload(JsonElement payload)
    {
        var array = payload.ValueKind == JsonValueKind.Array
            ? payload
            : payload.TryGetProperty("pending", out var pendingApprovals) && pendingApprovals.ValueKind == JsonValueKind.Array
                ? pendingApprovals
                : default;
        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray().Select(ParsePendingApproval).ToArray();
    }

    public async Task ResolveApprovalAsync(
        string id,
        string decision,
        CancellationToken cancellationToken = default)
    {
        await this.RequestAsync("exec.approval.resolve", new { id, decision }, cancellationToken);
    }

    public async Task<IReadOnlyList<PairingRequest>> ListPairingRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        var devicePayload = await this.RequestAsync("device.pair.list", new { }, cancellationToken);
        var nodePayload = await this.RequestAsync("node.pair.list", new { }, cancellationToken);
        return ParsePairingPayload("device", devicePayload)
            .Concat(ParsePairingPayload("node", nodePayload))
            .ToArray();
    }

    public static IReadOnlyList<PairingRequest> ParsePairingPayload(string kind, JsonElement payload)
    {
        return ParsePairingList(kind, payload).ToArray();
    }

    public async Task ResolvePairingAsync(
        PairingRequest request,
        bool approve,
        CancellationToken cancellationToken = default)
    {
        var method = request.Kind switch
        {
            "device" => approve ? "device.pair.approve" : "device.pair.reject",
            "node" => approve ? "node.pair.approve" : "node.pair.reject",
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unsupported pairing kind"),
        };
        await this.RequestAsync(method, new { requestId = request.RequestId }, cancellationToken);
    }

    public async Task<JsonElement> RequestAsync(
        string method,
        object parameters,
        CancellationToken cancellationToken = default)
    {
        var socketSnapshot = this.socket;
        if (socketSnapshot is not { State: WebSocketState.Open })
        {
            throw new InvalidOperationException("Gateway WebSocket is not connected.");
        }

        var id = Interlocked.Increment(ref this.nextRequestId).ToString();
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

            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                pendingCompletion.TrySetException(new TimeoutException(
                    $"Gateway request '{method}' timed out after {this.requestTimeout.TotalSeconds:0.#} seconds."));
                return;
            }

            pendingCompletion.TrySetCanceled(cancellationToken.IsCancellationRequested
                ? cancellationToken
                : linkedCts.Token);
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

    private async Task SendConnectAsync(AppPreferences prefs, CancellationToken cancellationToken)
    {
        var authToken = string.IsNullOrWhiteSpace(prefs.DeviceToken) ? prefs.GatewayToken : prefs.DeviceToken;
        var auth = string.IsNullOrWhiteSpace(authToken)
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["token"] = authToken };
        var payload = await this.RequestAsync(
            "connect",
            new
            {
                minProtocol = GatewayProtocol.Version,
                maxProtocol = GatewayProtocol.Version,
                client = new
                {
                    id = "windows-companion",
                    version = "0.1.0",
                    platform = "windows",
                    mode = "operator",
                },
                role = "operator",
                scopes = new[]
                {
                    "operator.read",
                    "operator.write",
                    "operator.approvals",
                    "operator.pairing",
                },
                caps = Array.Empty<string>(),
                commands = Array.Empty<string>(),
                permissions = new { },
                auth,
                locale = Thread.CurrentThread.CurrentCulture.Name,
                userAgent = "openclaw-windows/0.1.0",
            },
            cancellationToken);

        if (payload.TryGetProperty("auth", out var authPayload) &&
            authPayload.TryGetProperty("deviceToken", out var deviceToken) &&
            deviceToken.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(deviceToken.GetString()))
        {
            await this.preferences.UpdateAsync(current => current with
            {
                DeviceToken = deviceToken.GetString(),
            }, cancellationToken);
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
                    disconnectReason = new IOException("Gateway WebSocket closed.");
                    break;
                }
                this.HandleFrame(json);
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
                disconnectReason ??= new IOException("Gateway WebSocket disconnected.");
                this.FailPending(disconnectReason);
                if (ReferenceEquals(this.socket, socketSnapshot))
                {
                    this.socket = null;
                    this.SetState(GatewayRealtimeState.Disconnected, disconnectReason.Message);
                }
            }
        }
    }

    private void HandleFrame(string json)
    {
        var frame = GatewayFrameReader.Deserialize(json);
        if (frame.Response is not null)
        {
            this.HandleResponse(frame.Response);
            return;
        }
        if (frame.Event is not null)
        {
            this.EventReceived?.Invoke(new GatewayRealtimeEvent(frame.Event.Event, frame.Event.Payload));
        }
    }

    private void HandleResponse(ResponseFrame response)
    {
        if (!this.pending.TryRemove(response.Id, out var completion))
        {
            return;
        }
        if (response.Ok)
        {
            using var emptyPayload = JsonDocument.Parse("{}");
            completion.TrySetResult(response.Payload ?? emptyPayload.RootElement.Clone());
            return;
        }

        var code = response.Error?.Code ?? "UNKNOWN";
        var message = response.Error?.Message ?? "Gateway request failed.";
        if (code.Contains("PAIR", StringComparison.OrdinalIgnoreCase))
        {
            this.SetState(GatewayRealtimeState.PairingRequired, message);
        }
        else if (code.Contains("AUTH", StringComparison.OrdinalIgnoreCase) ||
                 code.Contains("TOKEN", StringComparison.OrdinalIgnoreCase))
        {
            this.SetState(GatewayRealtimeState.AuthFailed, message);
        }
        completion.TrySetException(new GatewayRpcException(code, message));
    }

    private async Task SendJsonAsync(
        ClientWebSocket socketSnapshot,
        object payload,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, this.jsonOptions));
        await socketSnapshot.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
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

    private void SetState(GatewayRealtimeState state, string? reason)
    {
        this.State = state;
        this.StateChanged?.Invoke(state, reason);
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

    private static ChatMessage? ParseChatMessage(JsonElement element)
    {
        var role = ReadString(element, "role") ?? ReadString(element, "kind") ?? "message";
        var text =
            ReadString(element, "text") ??
            ReadString(element, "content") ??
            ReadString(element, "message") ??
            element.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : new ChatMessage(role, text);
    }

    private static PendingApproval ParsePendingApproval(JsonElement element)
    {
        var id = ReadString(element, "id") ?? ReadString(element, "requestId") ?? "";
        var command = ReadString(element, "command") ??
            ReadString(element, "rawCommand") ??
            ReadString(element, "commandPreview") ??
            ReadString(element, "systemRunPlan", "commandText") ??
            "";
        return new PendingApproval(
            id,
            command,
            ReadString(element, "cwd") ?? ReadString(element, "systemRunPlan", "cwd"),
            ReadString(element, "agentId") ?? ReadString(element, "systemRunPlan", "agentId"),
            ReadString(element, "sessionKey") ?? ReadString(element, "systemRunPlan", "sessionKey"));
    }

    private static IEnumerable<PairingRequest> ParsePairingList(string kind, JsonElement payload)
    {
        if (!payload.TryGetProperty("pending", out var pendingRequests) ||
            pendingRequests.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return pendingRequests.EnumerateArray().Select(entry =>
        {
            var requestId = ReadString(entry, "requestId") ?? "";
            var deviceId = ReadString(entry, "deviceId") ?? ReadString(entry, "nodeId") ?? "";
            var displayName = ReadString(entry, "displayName") ?? deviceId;
            return new PairingRequest(requestId, kind, displayName, deviceId);
        }).ToArray();
    }

    private static string? ReadString(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}
