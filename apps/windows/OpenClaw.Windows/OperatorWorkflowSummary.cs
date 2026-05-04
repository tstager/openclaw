namespace OpenClaw.Windows;

public sealed record OperatorWorkflowSummary(
    int PendingApprovals,
    int PendingPairingRequests,
    string PairingReadiness)
{
    public static OperatorWorkflowSummary Empty { get; } = new(0, 0, "Not checked");

    public string ApprovalsStatus => this.PendingApprovals switch
    {
        0 => "No approvals pending",
        1 => "1 approval pending",
        _ => $"{this.PendingApprovals} approvals pending",
    };

    public string PairingStatus => this.PendingPairingRequests switch
    {
        0 => this.PairingReadiness,
        1 => "1 pairing request pending",
        _ => $"{this.PendingPairingRequests} pairing requests pending",
    };

    public static OperatorWorkflowSummary Create(
        IReadOnlyCollection<PendingApproval> approvals,
        IReadOnlyCollection<PairingRequest> pairingRequests,
        GatewayRealtimeState realtimeState)
    {
        var readiness = realtimeState switch
        {
            GatewayRealtimeState.Connected => pairingRequests.Count == 0
                ? "Ready for pairing requests"
                : "Pairing action needed",
            GatewayRealtimeState.PairingRequired => "Pairing required",
            GatewayRealtimeState.Connecting => "Checking pairing readiness",
            GatewayRealtimeState.AuthFailed => "Authentication required",
            _ => "Connect to check pairing",
        };
        return new OperatorWorkflowSummary(approvals.Count, pairingRequests.Count, readiness);
    }
}
