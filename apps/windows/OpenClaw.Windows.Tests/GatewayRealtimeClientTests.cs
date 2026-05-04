using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class GatewayRealtimeClientTests
{
    [TestMethod]
    public void ParsesChatHistoryPayload()
    {
        using var document = JsonDocument.Parse(
            """{"messages":[{"role":"user","text":"hello"},{"role":"assistant","content":"hi"}]}""");

        var messages = GatewayRealtimeClient.ParseChatHistoryPayload(document.RootElement);

        Assert.HasCount(2, messages);
        Assert.AreEqual("user", messages[0].Role);
        Assert.AreEqual("hello", messages[0].Text);
        Assert.AreEqual("assistant", messages[1].Role);
        Assert.AreEqual("hi", messages[1].Text);
    }

    [TestMethod]
    public void ParsesPendingApprovals()
    {
        using var document = JsonDocument.Parse(
            """{"pending":[{"id":"approval-1","systemRunPlan":{"commandText":"pwsh -NoProfile","cwd":"C:\\repo","agentId":"main","sessionKey":"agent:main"}}]}""");

        var approvals = GatewayRealtimeClient.ParseApprovalsPayload(document.RootElement);

        Assert.HasCount(1, approvals);
        Assert.AreEqual("approval-1", approvals[0].Id);
        Assert.AreEqual("pwsh -NoProfile", approvals[0].Command);
        Assert.AreEqual(@"C:\repo", approvals[0].Cwd);
        Assert.AreEqual("main", approvals[0].AgentId);
    }

    [TestMethod]
    public void ParsesPairingPayload()
    {
        using var document = JsonDocument.Parse(
            """{"pending":[{"requestId":"pair-1","deviceId":"device-1","displayName":"Windows laptop"}]}""");

        var requests = GatewayRealtimeClient.ParsePairingPayload("device", document.RootElement);

        Assert.HasCount(1, requests);
        Assert.AreEqual("pair-1", requests[0].RequestId);
        Assert.AreEqual("device", requests[0].Kind);
        Assert.AreEqual("Windows laptop", requests[0].DisplayName);
        Assert.AreEqual("device-1", requests[0].DeviceId);
    }

    [TestMethod]
    public async Task RequestAsyncTimesOutPendingRequest()
    {
        await using var server = GatewayRealtimeTestServer.Start(async (socket, request) =>
        {
            using var document = JsonDocument.Parse(request);
            var frame = document.RootElement;
            if (ReadString(frame, "method") == "connect")
            {
                await SendOkResponseAsync(socket, ReadString(frame, "id") ?? "");
            }
        });
        var client = CreateClient(server.WebSocketUrl, TimeSpan.FromMilliseconds(100));

        await client.ConnectAsync();
        var exception = await ThrowsAsync<TimeoutException>(async () =>
            await client.LoadChatHistoryAsync("main"));

        StringAssert.Contains(exception.Message, "chat.history");
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestAsyncFailsPendingRequestWhenSocketCloses()
    {
        await using var server = GatewayRealtimeTestServer.Start(async (socket, request) =>
        {
            using var document = JsonDocument.Parse(request);
            var frame = document.RootElement;
            var method = ReadString(frame, "method");
            if (method == "connect")
            {
                await SendOkResponseAsync(socket, ReadString(frame, "id") ?? "");
                return;
            }

            if (method == "chat.history")
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.EndpointUnavailable,
                    "test close",
                    CancellationToken.None);
            }
        });
        var client = CreateClient(server.WebSocketUrl, TimeSpan.FromSeconds(5));

        await client.ConnectAsync();
        var exception = await ThrowsAsync<IOException>(async () =>
            await client.LoadChatHistoryAsync("main"));

        StringAssert.Contains(exception.Message, "closed");
        Assert.AreEqual(GatewayRealtimeState.Disconnected, client.State);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task RequestAsyncFailsPendingRequestWhenFrameIsMalformed()
    {
        await using var server = GatewayRealtimeTestServer.Start(async (socket, request) =>
        {
            using var document = JsonDocument.Parse(request);
            var frame = document.RootElement;
            var method = ReadString(frame, "method");
            if (method == "connect")
            {
                await SendOkResponseAsync(socket, ReadString(frame, "id") ?? "");
                return;
            }

            if (method == "chat.history")
            {
                await SendTextAsync(socket, """{"type":"res",""");
            }
        });
        var client = CreateClient(server.WebSocketUrl, TimeSpan.FromSeconds(5));

        await client.ConnectAsync();
        await ThrowsAsync<JsonException>(async () =>
            await client.LoadChatHistoryAsync("main"));

        Assert.AreEqual(GatewayRealtimeState.Disconnected, client.State);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ConnectAsyncSendsStoredDeviceTokenAsDeviceTokenAndSignedDeviceIdentity()
    {
        var connectRequest = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = GatewayRealtimeTestServer.Start(
            async (socket, request) =>
            {
                using var document = JsonDocument.Parse(request);
                var frame = document.RootElement;
                if (ReadString(frame, "method") == "connect")
                {
                    connectRequest.TrySetResult(frame.Clone());
                    await SendOkResponseAsync(socket, ReadString(frame, "id") ?? "", "issued-device-token");
                }
            },
            challengeNonce: "nonce-1");
        var credentials = new InMemoryAppCredentialStore();
        await credentials.SaveDeviceTokenAsync("stored-device-token");
        var client = CreateClient(
            server.WebSocketUrl,
            TimeSpan.FromSeconds(5),
            credentials,
            new DeviceIdentityStore(credentials));

        await client.ConnectAsync();

        var connect = await connectRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var parameters = connect.GetProperty("params");
        var clientPayload = parameters.GetProperty("client");
        Assert.AreEqual("openclaw-windows", clientPayload.GetProperty("id").GetString());
        Assert.AreEqual("ui", clientPayload.GetProperty("mode").GetString());
        Assert.AreEqual("operator", parameters.GetProperty("role").GetString());
        var auth = parameters.GetProperty("auth");
        Assert.AreEqual("stored-device-token", auth.GetProperty("deviceToken").GetString());
        Assert.IsFalse(auth.TryGetProperty("token", out var token) && token.GetString() == "stored-device-token");
        var device = parameters.GetProperty("device");
        Assert.IsFalse(string.IsNullOrWhiteSpace(device.GetProperty("id").GetString()));
        Assert.IsFalse(string.IsNullOrWhiteSpace(device.GetProperty("publicKey").GetString()));
        Assert.IsFalse(string.IsNullOrWhiteSpace(device.GetProperty("signature").GetString()));
        Assert.AreEqual("nonce-1", device.GetProperty("nonce").GetString());
        Assert.IsGreaterThan(0, device.GetProperty("signedAt").GetInt64());
        Assert.AreEqual("issued-device-token", await credentials.LoadDeviceTokenAsync());
        await client.DisposeAsync();
    }

    private static GatewayRealtimeClient CreateClient(string gatewayUrl, TimeSpan requestTimeout)
    {
        return CreateClient(
            gatewayUrl,
            requestTimeout,
            new InMemoryAppCredentialStore(),
            deviceIdentityStore: null);
    }

    private static GatewayRealtimeClient CreateClient(
        string gatewayUrl,
        TimeSpan requestTimeout,
        IAppCredentialStore credentials,
        DeviceIdentityStore? deviceIdentityStore)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        var store = new AppPreferencesStore(path, credentials);
        var current = store.LoadAsync().GetAwaiter().GetResult();
        store.SaveAsync(current with
        {
            GatewayUrl = gatewayUrl,
        }).GetAwaiter().GetResult();
        return deviceIdentityStore is null
            ? new GatewayRealtimeClient(store, requestTimeout)
            : new GatewayRealtimeClient(store, deviceIdentityStore, requestTimeout);
    }

    private static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException exception)
        {
            return exception;
        }

        Assert.Fail($"Expected exception of type {typeof(TException).FullName}.");
        throw new InvalidOperationException("Unreachable assertion path.");
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static Task SendOkResponseAsync(WebSocket socket, string id, string? deviceToken = null)
    {
        var deviceTokenJson = deviceToken is null ? "" : $",\"deviceToken\":\"{deviceToken}\"";
        return SendTextAsync(
            socket,
            $"{{\"type\":\"res\",\"id\":\"{id}\",\"ok\":true,\"payload\":{{\"auth\":{{\"role\":\"operator\",\"scopes\":[]{deviceTokenJson}}}}}}}");
    }

    private static async Task SendTextAsync(WebSocket socket, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private sealed class GatewayRealtimeTestServer : IAsyncDisposable
    {
        private readonly Func<WebSocket, string, Task> handleRequest;
        private readonly string? challengeNonce;
        private readonly HttpListener listener = new();
        private readonly Task serverTask;

        private GatewayRealtimeTestServer(
            int port,
            Func<WebSocket, string, Task> handleRequest,
            string? challengeNonce)
        {
            this.handleRequest = handleRequest;
            this.challengeNonce = challengeNonce;
            this.WebSocketUrl = $"ws://127.0.0.1:{port}/gateway/";
            this.listener.Prefixes.Add($"http://127.0.0.1:{port}/gateway/");
            this.listener.Start();
            this.serverTask = this.RunAsync();
        }

        public string WebSocketUrl { get; }

        public static GatewayRealtimeTestServer Start(
            Func<WebSocket, string, Task> handleRequest,
            string? challengeNonce = null)
        {
            return new GatewayRealtimeTestServer(GetAvailablePort(), handleRequest, challengeNonce);
        }

        public async ValueTask DisposeAsync()
        {
            this.listener.Stop();
            try
            {
                await this.serverTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
            this.listener.Close();
        }

        private async Task RunAsync()
        {
            try
            {
                var context = await this.listener.GetContextAsync();
                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.Close();
                    return;
                }

                var webSocketContext = await context.AcceptWebSocketAsync(null);
                using var socket = webSocketContext.WebSocket;
                if (!string.IsNullOrWhiteSpace(this.challengeNonce))
                {
                    await SendTextAsync(
                        socket,
                        $"{{\"type\":\"event\",\"event\":\"connect.challenge\",\"payload\":{{\"nonce\":\"{this.challengeNonce}\",\"ts\":1}}}}");
                }
                while (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    var request = await ReceiveTextAsync(socket);
                    if (request is null)
                    {
                        return;
                    }
                    await this.handleRequest(socket, request);
                }
            }
            catch (HttpListenerException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static int GetAvailablePort()
        {
            using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            return ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        }

        private static async Task<string?> ReceiveTextAsync(WebSocket socket)
        {
            var buffer = new byte[64 * 1024];
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }
                stream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    private sealed class InMemoryAppCredentialStore : IAppCredentialStore
    {
        private string? gatewayToken;
        private string? deviceToken;
        private string? devicePrivateKey;

        public Task<string?> LoadGatewayTokenAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.gatewayToken);
        }

        public Task SaveGatewayTokenAsync(string? token, CancellationToken cancellationToken = default)
        {
            this.gatewayToken = token;
            return Task.CompletedTask;
        }

        public Task<string?> LoadDeviceTokenAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.deviceToken);
        }

        public Task SaveDeviceTokenAsync(string? token, CancellationToken cancellationToken = default)
        {
            this.deviceToken = token;
            return Task.CompletedTask;
        }

        public Task<string?> LoadDevicePrivateKeyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.devicePrivateKey);
        }

        public Task SaveDevicePrivateKeyAsync(string? privateKey, CancellationToken cancellationToken = default)
        {
            this.devicePrivateKey = privateKey;
            return Task.CompletedTask;
        }
    }
}
