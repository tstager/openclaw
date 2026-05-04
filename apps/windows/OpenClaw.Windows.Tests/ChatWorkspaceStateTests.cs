using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class ChatWorkspaceStateTests
{
    [TestMethod]
    public void ConnectedRealtimeWithoutMessagesShowsEmptyState()
    {
        var state = new ChatWorkspaceState();

        state.ApplyRealtimeState(GatewayRealtimeState.Connected, null);

        Assert.AreEqual(ChatWorkspaceStatus.Empty, state.Status);
        Assert.AreEqual("No messages in this session yet.", state.StatusDetail);
    }

    [TestMethod]
    public void LoadedMessagesShowConnectedState()
    {
        var state = new ChatWorkspaceState();
        var messages = new[] { new ChatMessage("assistant", "Ready.") };

        state.ApplyMessages(messages, GatewayRealtimeState.Connected);

        Assert.AreEqual(ChatWorkspaceStatus.Connected, state.Status);
        Assert.AreEqual("Connected to the Gateway.", state.StatusDetail);
        CollectionAssert.AreEqual(messages, state.Messages.ToArray());
        Assert.IsNotNull(state.LastLoadedAt);
    }

    [TestMethod]
    public void DisconnectedRealtimeKeepsClearReason()
    {
        var state = new ChatWorkspaceState();

        state.ApplyRealtimeState(GatewayRealtimeState.AuthFailed, "Token rejected.");

        Assert.AreEqual(ChatWorkspaceStatus.Disconnected, state.Status);
        Assert.AreEqual("Token rejected.", state.StatusDetail);
    }

    [TestMethod]
    public void SendingStateIsExplicitUntilMessagesOrFailureArrive()
    {
        var state = new ChatWorkspaceState();

        state.StartSending();
        state.ApplyRealtimeState(GatewayRealtimeState.Disconnected, "Network closed.");

        Assert.AreEqual(ChatWorkspaceStatus.Sending, state.Status);
        Assert.AreEqual("Sending message...", state.StatusDetail);
    }

    [TestMethod]
    public void FailureStateSurfacesErrorMessage()
    {
        var state = new ChatWorkspaceState();

        state.ApplyFailure(new InvalidOperationException("Gateway WebSocket is not connected."));

        Assert.AreEqual(ChatWorkspaceStatus.Failed, state.Status);
        Assert.AreEqual("Gateway WebSocket is not connected.", state.StatusDetail);
    }
}
