using System.Collections.Concurrent;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenClaw.Protocol;
using OpenClaw.Protocol.Generated;

namespace OpenClaw.Windows;

/// <summary>
/// Coarse connection state for the gateway WebSocket channel.
/// </summary>
public enum GatewayRealtimeState
{
    Disconnected,
    Connecting,
    Connected,
    PairingRequired,
    AuthFailed,
}

/// <summary>
/// Gateway event delivered to the shell after frame parsing.
/// </summary>
public sealed record GatewayRealtimeEvent(string Name, JsonElement? Payload);

/// <summary>
/// Authorization returned by the gateway connect call and reduced to a UI capability label.
/// </summary>
public sealed record GatewayRealtimeAuthorization(string? Role, IReadOnlyList<string> Scopes)
{
    public string Capability
    {
        get
        {
            if (this.Scopes.Contains("operator.admin", StringComparer.Ordinal))
            {
                return "admin_capable";
            }
            if (this.Scopes.Contains("operator.write", StringComparer.Ordinal))
            {
                return "write_capable";
            }
            if (this.Scopes.Contains("operator.read", StringComparer.Ordinal))
            {
                return "read_only";
            }
            return "unknown";
        }
    }
}

/// <summary>
/// Chat transcript row shown in the Chat page.
/// </summary>
public sealed record ChatMessage(string Role, string Text);

/// <summary>
/// Session row shown by the Windows session browser.
/// </summary>
public sealed record SessionSummary(
    string Key,
    string DisplayName,
    string Kind,
    string? AgentId,
    string? Channel,
    string? Status,
    bool HasActiveRun,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Pending command approval surfaced through the operator Approvals page.
/// </summary>
public sealed record PendingApproval(
    string Id,
    string Command,
    string? Cwd,
    string? AgentId,
    string? SessionKey);

/// <summary>
/// Pending device or node pairing request.
/// </summary>
public sealed record PairingRequest(
    string RequestId,
    string Kind,
    string DisplayName,
    string DeviceId);

/// <summary>
/// Exception raised when the gateway returns an RPC error frame.
/// </summary>
public sealed class GatewayRpcException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// Maintains the realtime WebSocket connection and exposes gateway RPC helpers for the WinUI shell.
/// </summary>
public sealed class GatewayRealtimeClient : IAsyncDisposable
{
    // The gateway currently accepts known client ids; macOS is used until a Windows id lands upstream.
    private const string ClientId = "openclaw-macos";
    private const string ClientMode = "ui";
    private const string ClientRole = "operator";
    private static readonly string[] RequestedScopes =
    [
        "operator.read",
        "operator.write",
        "operator.approvals",
        "operator.pairing",
    ];
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DisconnectCloseTimeout = TimeSpan.FromSeconds(2);
    private readonly TimeSpan requestTimeout;
    private readonly AppPreferencesStore preferences;
    private readonly DeviceIdentityStore? deviceIdentityStore;
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> pending = new();
    private readonly JsonSerializerOptions jsonOptions = GatewayProtocolJson.SerializerOptions;
    private ClientWebSocket? socket;
    private CancellationTokenSource? receiveCts;
    private Task? receiveTask;
    private TaskCompletionSource<string>? connectChallenge;
    private int nextRequestId;

    public GatewayRealtimeClient(AppPreferencesStore preferences, TimeSpan? requestTimeout = null)
        : this(preferences, deviceIdentityStore: null, requestTimeout)
    {
    }

    public GatewayRealtimeClient(
        AppPreferencesStore preferences,
        DeviceIdentityStore? deviceIdentityStore,
        TimeSpan? requestTimeout = null)
    {
        this.preferences = preferences;
        this.deviceIdentityStore = deviceIdentityStore;
        this.requestTimeout = requestTimeout ?? DefaultRequestTimeout;
    }

    public GatewayRealtimeState State { get; private set; } = GatewayRealtimeState.Disconnected;

    public GatewayRealtimeAuthorization? Authorization { get; private set; }

    public event Action<GatewayRealtimeEvent>? EventReceived;

    public event Action<GatewayRealtimeState, string?>? StateChanged;

    /// <summary>
    /// Loads current preferences and starts the realtime connection.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var prefs = await this.preferences.LoadAsync(cancellationToken);
        await this.ConnectAsync(prefs, cancellationToken);
    }

    public async Task ConnectAsync(AppPreferences prefs, CancellationToken cancellationToken = default)
    {
        await this.ConnectAsync(prefs, repairNarrowDeviceIdentity: true, cancellationToken);
    }

    /// <summary>
    /// Opens the WebSocket, completes the connect handshake, and repairs stale narrow device identity once.
    /// </summary>
    private async Task ConnectAsync(
        AppPreferences prefs,
        bool repairNarrowDeviceIdentity,
        CancellationToken cancellationToken)
    {
        await this.DisconnectAsync();
        this.SetState(GatewayRealtimeState.Connecting, null);
        var connectingSocket = new ClientWebSocket();
        try
        {
            this.socket = connectingSocket;
            await connectingSocket.ConnectAsync(new Uri(prefs.GatewayUrl), cancellationToken);
            this.receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.connectChallenge = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.receiveTask = this.ReceiveLoopAsync(connectingSocket, this.receiveCts.Token);
            await this.SendConnectAsync(prefs, cancellationToken);
            if (repairNarrowDeviceIdentity &&
                await this.TryRepairNarrowDeviceIdentityAsync(prefs, cancellationToken))
            {
                await this.DisconnectAsync();
                var refreshedPrefs = await this.preferences.LoadAsync(cancellationToken);
                await this.ConnectAsync(refreshedPrefs, repairNarrowDeviceIdentity: false, cancellationToken);
                return;
            }
            this.connectChallenge = null;
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

    /// <summary>
    /// Closes the socket, cancels the receive loop, and fails any outstanding RPC requests.
    /// </summary>
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
        this.Authorization = null;
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
        this.sendGate.Dispose();
    }

    /// <summary>
    /// Reads recent chat messages from the gateway for the selected session.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> LoadChatHistoryAsync(
        string sessionKey,
        CancellationToken cancellationToken = default)
    {
        var payload = await this.RequestAsync("chat.history", new { sessionKey, limit = 80 }, cancellationToken);
        return ParseChatHistoryPayload(payload);
    }

    /// <summary>
    /// Sends a chat message with a Windows-specific idempotency key.
    /// </summary>
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

    /// <summary>
    /// Lists gateway sessions so the Windows shell can select the active chat target.
    /// </summary>
    public async Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var payload = await this.RequestAsync(
            "sessions.list",
            new { limit = 100, includeDerivedTitles = true, includeLastMessage = true },
            cancellationToken);
        return ParseSessionsListPayload(payload);
    }

    /// <summary>
    /// Requests pending command approvals from the gateway.
    /// </summary>
    public async Task<IReadOnlyList<PendingApproval>> ListApprovalsAsync(CancellationToken cancellationToken = default)
    {
        var payload = await this.RequestAsync("exec.approval.list", new { }, cancellationToken);
        return ParseApprovalsPayload(payload);
    }

    /// <summary>
    /// Accepts multiple historical chat payload shapes from gateway versions.
    /// </summary>
    public static IReadOnlyList<ChatMessage> ParseChatHistoryPayload(JsonElement payload)
    {
        if (!payload.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return messages.EnumerateArray()
            .Select(ChatTranscriptProjector.Project)
            .Where(static message => message is not null)
            .Cast<ChatMessage>()
            .ToArray();
    }

    /// <summary>
    /// Accepts both array and wrapped pending-approval payloads.
    /// </summary>
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

    /// <summary>
    /// Accepts the gateway sessions.list payload and keeps only fields used by the Windows browser.
    /// </summary>
    public static IReadOnlyList<SessionSummary> ParseSessionsListPayload(JsonElement payload)
    {
        var array = payload.ValueKind == JsonValueKind.Array
            ? payload
            : payload.TryGetProperty("sessions", out var sessions) && sessions.ValueKind == JsonValueKind.Array
                ? sessions
                : default;
        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(ParseSessionSummary)
            .Where(static session => session is not null)
            .Cast<SessionSummary>()
            .ToArray();
    }

    /// <summary>
    /// Approves or rejects one pending command approval.
    /// </summary>
    public async Task ResolveApprovalAsync(
        string id,
        string decision,
        CancellationToken cancellationToken = default)
    {
        await this.RequestAsync("exec.approval.resolve", new { id, decision }, cancellationToken);
    }

    /// <summary>
    /// Combines device and node pairing queues into one UI list.
    /// </summary>
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

    /// <summary>
    /// Routes approval/rejection to the correct gateway method for device or node requests.
    /// </summary>
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

    /// <summary>
    /// Sends one gateway request frame and waits for the matching response id.
    /// </summary>
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

    /// <summary>
    /// Builds the gateway connect payload, including shared token, device token, and signed device identity.
    /// </summary>
    private async Task SendConnectAsync(AppPreferences prefs, CancellationToken cancellationToken)
    {
        var scopes = RequestedScopes;
        var sharedToken = NormalizeToken(prefs.GatewayToken);
        var deviceToken = NormalizeToken(prefs.DeviceToken);
        var auth = new Dictionary<string, string>();
        if (sharedToken is not null)
        {
            auth["token"] = sharedToken;
        }
        if (deviceToken is not null)
        {
            auth["deviceToken"] = deviceToken;
        }
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
            ["scopes"] = scopes,
            ["caps"] = Array.Empty<string>(),
            ["commands"] = Array.Empty<string>(),
            ["permissions"] = new { },
            ["locale"] = Thread.CurrentThread.CurrentCulture.Name,
            ["userAgent"] = "openclaw-windows/0.1.0",
        };
        if (auth.Count > 0)
        {
            connectParams["auth"] = auth;
        }
        if (this.deviceIdentityStore is not null)
        {
            var identity = await this.deviceIdentityStore.LoadOrCreateAsync(cancellationToken);
            var nonce = await this.WaitForConnectChallengeAsync(cancellationToken);
            var signedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var signedPayload = DeviceIdentityStore.BuildDeviceAuthPayloadV3(
                identity.DeviceId,
                ClientId,
                ClientMode,
                ClientRole,
                scopes,
                signedAt,
                sharedToken ?? deviceToken,
                nonce,
                "windows",
                deviceFamily: null);
            connectParams["device"] = new
            {
                id = identity.DeviceId,
                publicKey = identity.PublicKeyPem,
                signature = identity.SignPayload(signedPayload),
                signedAt,
                nonce,
            };
        }

        var responsePayload = await this.RequestAsync(
            "connect",
            connectParams,
            cancellationToken);

        if (responsePayload.TryGetProperty("auth", out var authPayload))
        {
            this.Authorization = ParseAuthorization(authPayload);
            if (authPayload.TryGetProperty("deviceToken", out var issuedDeviceToken) &&
                issuedDeviceToken.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(issuedDeviceToken.GetString()))
            {
                await this.preferences.UpdateAsync(current => current with
                {
                    DeviceToken = issuedDeviceToken.GetString(),
                }, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Clears stale device identity when a shared token is present but the device reconnects with narrow scopes.
    /// </summary>
    private async Task<bool> TryRepairNarrowDeviceIdentityAsync(
        AppPreferences prefs,
        CancellationToken cancellationToken)
    {
        if (this.deviceIdentityStore is null ||
            string.IsNullOrWhiteSpace(prefs.GatewayToken) ||
            this.Authorization is not { } authorization ||
            authorization.Scopes.Count == 0 ||
            HasRequestedScopes(authorization.Scopes))
        {
            return false;
        }

        await this.deviceIdentityStore.ResetAsync(cancellationToken);
        return true;
    }

    private static bool HasRequestedScopes(IReadOnlyList<string> scopes)
    {
        if (scopes.Contains("operator.admin", StringComparer.Ordinal))
        {
            return true;
        }
        return RequestedScopes.All(scope => scopes.Contains(scope, StringComparer.Ordinal));
    }

    private static GatewayRealtimeAuthorization ParseAuthorization(JsonElement authPayload)
    {
        var scopes = authPayload.TryGetProperty("scopes", out var scopesPayload) &&
            scopesPayload.ValueKind == JsonValueKind.Array
                ? scopesPayload.EnumerateArray()
                    .Where(static scope => scope.ValueKind == JsonValueKind.String)
                    .Select(static scope => scope.GetString())
                    .Where(static scope => !string.IsNullOrWhiteSpace(scope))
                    .Cast<string>()
                    .ToArray()
                : [];
        return new GatewayRealtimeAuthorization(ReadString(authPayload, "role"), scopes);
    }

    /// <summary>
    /// Continuously receives gateway frames and turns disconnects into state/error updates.
    /// </summary>
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

    /// <summary>
    /// Dispatches response frames to pending requests and event frames to UI subscribers.
    /// </summary>
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
            this.HandleEvent(frame.Event);
            this.EventReceived?.Invoke(new GatewayRealtimeEvent(frame.Event.Event, frame.Event.Payload));
        }
    }

    /// <summary>
    /// Captures the connect challenge nonce needed for signed device authentication.
    /// </summary>
    private void HandleEvent(EventFrame @event)
    {
        if (@event.Event != "connect.challenge" ||
            @event.Payload is not { } payload ||
            !payload.TryGetProperty("nonce", out var nonce) ||
            nonce.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(nonce.GetString()))
        {
            return;
        }

        this.connectChallenge?.TrySetResult(nonce.GetString()!);
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

    /// <summary>
    /// Serializes WebSocket writes because ClientWebSocket does not support concurrent sends.
    /// </summary>
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
                throw new IOException(FormatWebSocketCloseMessage(
                    "Gateway WebSocket closed",
                    result.CloseStatus,
                    result.CloseStatusDescription));
            }
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string FormatWebSocketCloseMessage(
        string prefix,
        WebSocketCloseStatus? status,
        string? description)
    {
        var message = status is null ? prefix : $"{prefix}: {status}";
        return string.IsNullOrWhiteSpace(description) ? message : $"{message} - {description}";
    }

    private void SetState(GatewayRealtimeState state, string? reason)
    {
        this.State = state;
        this.StateChanged?.Invoke(state, reason);
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

    private static string? NormalizeToken(string? token)
    {
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
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

    private static SessionSummary? ParseSessionSummary(JsonElement element)
    {
        var key = ReadString(element, "key") ?? ReadString(element, "sessionKey");
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var displayName =
            ReadString(element, "derivedTitle") ??
            ReadString(element, "displayName") ??
            ReadString(element, "label") ??
            ReadString(element, "lastMessagePreview") ??
            key;
        return new SessionSummary(
            key,
            displayName,
            ReadString(element, "kind") ?? "unknown",
            ResolveAgentId(key),
            ReadString(element, "channel") ?? ReadString(element, "lastChannel"),
            ReadString(element, "status"),
            ReadBool(element, "hasActiveRun") == true,
            ReadUnixTimeMilliseconds(element, "updatedAt"));
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

    private static PairingRequest[] ParsePairingList(string kind, JsonElement payload)
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

    private static bool? ReadBool(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static DateTimeOffset? ReadUnixTimeMilliseconds(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        if (current.ValueKind != JsonValueKind.Number || !current.TryGetInt64(out var value))
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? ResolveAgentId(string sessionKey)
    {
        var parts = sessionKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && string.Equals(parts[0], "agent", StringComparison.Ordinal)
            ? parts[1]
            : null;
    }
}
