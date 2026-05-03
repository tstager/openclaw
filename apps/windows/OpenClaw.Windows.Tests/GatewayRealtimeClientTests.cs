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

    private static GatewayRealtimeClient CreateClient(string gatewayUrl, TimeSpan requestTimeout)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "preferences.json");
        var store = new AppPreferencesStore(path);
        store.SaveAsync(AppPreferences.Default with
        {
            GatewayUrl = gatewayUrl,
        }).GetAwaiter().GetResult();
        return new GatewayRealtimeClient(store, requestTimeout);
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

    private static Task SendOkResponseAsync(WebSocket socket, string id)
    {
        return SendTextAsync(
            socket,
            $"{{\"type\":\"res\",\"id\":\"{id}\",\"ok\":true,\"payload\":{{\"auth\":{{\"role\":\"operator\",\"scopes\":[]}}}}}}");
    }

    private static async Task SendTextAsync(WebSocket socket, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private sealed class GatewayRealtimeTestServer : IAsyncDisposable
    {
        private readonly Func<WebSocket, string, Task> handleRequest;
        private readonly HttpListener listener = new();
        private readonly Task serverTask;

        private GatewayRealtimeTestServer(int port, Func<WebSocket, string, Task> handleRequest)
        {
            this.handleRequest = handleRequest;
            this.WebSocketUrl = $"ws://127.0.0.1:{port}/gateway/";
            this.listener.Prefixes.Add($"http://127.0.0.1:{port}/gateway/");
            this.listener.Start();
            this.serverTask = this.RunAsync();
        }

        public string WebSocketUrl { get; }

        public static GatewayRealtimeTestServer Start(Func<WebSocket, string, Task> handleRequest)
        {
            return new GatewayRealtimeTestServer(GetAvailablePort(), handleRequest);
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
}
