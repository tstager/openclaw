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
}
