namespace OpenClaw.Windows;

public enum ChatWorkspaceStatus
{
    Empty,
    Disconnected,
    Connected,
    Sending,
    Failed,
}

public sealed class ChatWorkspaceState
{
    public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];

    public ChatWorkspaceStatus Status { get; private set; } = ChatWorkspaceStatus.Disconnected;

    public string? StatusDetail { get; private set; } = "Gateway realtime connection is disconnected.";

    public DateTimeOffset? LastLoadedAt { get; private set; }

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

    public void StartSending()
    {
        this.Status = ChatWorkspaceStatus.Sending;
        this.StatusDetail = "Sending message...";
    }

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
