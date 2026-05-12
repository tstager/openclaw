namespace OpenClaw.Windows;

/// <summary>
/// Tracks the local chat view state independently from the realtime gateway connection state.
/// </summary>
public enum ChatWorkspaceStatus
{
    Empty,
    Disconnected,
    Connected,
    Sending,
    Failed,
}

/// <summary>
/// Keeps the chat page's message list and status text in sync with gateway refreshes and send operations.
/// </summary>
public sealed class ChatWorkspaceState
{
    public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];

    public ChatWorkspaceStatus Status { get; private set; } = ChatWorkspaceStatus.Disconnected;

    public string? StatusDetail { get; private set; } = "Gateway realtime connection is disconnected.";

    public DateTimeOffset? LastLoadedAt { get; private set; }

    /// <summary>
    /// Applies connection state changes without interrupting an in-flight send operation.
    /// </summary>
    public void ApplyRealtimeState(GatewayRealtimeState realtimeState, string? reason)
    {
        if (this.Status == ChatWorkspaceStatus.Sending)
        {
            return;
        }

        if (realtimeState == GatewayRealtimeState.Connected)
        {
            this.Status = this.Messages.Count == 0 ? ChatWorkspaceStatus.Empty : ChatWorkspaceStatus.Connected;
            this.StatusDetail = this.Messages.Count == 0
                ? "No messages in this session yet."
                : "Connected to the Gateway.";
            return;
        }

        this.Status = ChatWorkspaceStatus.Disconnected;
        this.StatusDetail = string.IsNullOrWhiteSpace(reason)
            ? RealtimeStateDetail(realtimeState)
            : reason;
    }

    /// <summary>
    /// Marks the workspace busy while the UI waits for the gateway RPC send call to finish.
    /// </summary>
    public void StartSending()
    {
        this.Status = ChatWorkspaceStatus.Sending;
        this.StatusDetail = "Sending message...";
    }

    /// <summary>
    /// Replaces the displayed transcript after a gateway session read.
    /// </summary>
    public void ApplyMessages(IReadOnlyList<ChatMessage> messages, GatewayRealtimeState realtimeState)
    {
        this.Messages = messages;
        this.LastLoadedAt = DateTimeOffset.UtcNow;
        if (realtimeState == GatewayRealtimeState.Connected)
        {
            this.Status = messages.Count == 0 ? ChatWorkspaceStatus.Empty : ChatWorkspaceStatus.Connected;
            this.StatusDetail = messages.Count == 0
                ? "No messages in this session yet."
                : "Connected to the Gateway.";
            return;
        }

        this.Status = ChatWorkspaceStatus.Disconnected;
        this.StatusDetail = RealtimeStateDetail(realtimeState);
    }

    /// <summary>
    /// Surfaces the last gateway or UI exception as the chat status detail.
    /// </summary>
    public void ApplyFailure(Exception exception)
    {
        this.Status = ChatWorkspaceStatus.Failed;
        this.StatusDetail = exception.Message;
    }

    private static string RealtimeStateDetail(GatewayRealtimeState state)
    {
        return state switch
        {
            GatewayRealtimeState.Connecting => "Connecting to the Gateway.",
            GatewayRealtimeState.PairingRequired => "Pairing is required before chat can load.",
            GatewayRealtimeState.AuthFailed => "Gateway authentication failed.",
            _ => "Gateway realtime connection is disconnected.",
        };
    }
}
