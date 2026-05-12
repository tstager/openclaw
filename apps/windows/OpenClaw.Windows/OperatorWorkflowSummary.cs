namespace OpenClaw.Windows;

/// <summary>
/// Compact counts and readiness text for approval and pairing operator workflows.
/// </summary>
public sealed record OperatorWorkflowSummary(
    int PendingApprovals,
    int PendingPairingRequests,
    string PairingReadiness)
{
    /// <summary>
    /// Empty state used before the app has fetched approvals or pairing requests.
    /// </summary>
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

    /// <summary>
    /// Summarizes current realtime workflow data into Home page status rows.
    /// </summary>
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
