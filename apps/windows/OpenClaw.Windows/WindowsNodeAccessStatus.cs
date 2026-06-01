namespace OpenClaw.Windows;

/// <summary>
/// Display-ready view of the Windows companion's operator scope grant and node pairing access, used by the
/// Home, Pairing, Devices, Logs, and Settings surfaces to make full-access state obvious and repairable.
/// </summary>
public sealed record WindowsNodeAccessStatus(
    string Capability,
    IReadOnlyList<string> GrantedScopes,
    IReadOnlyList<string> MissingScopes,
    string ScopeSummary,
    bool OperatorConnected,
    bool NodeConnected,
    bool RequiresAdminForNode,
    bool RepairAvailable,
    string NodeStateLabel,
    string Explanation)
{
    public static WindowsNodeAccessStatus Create(
        GatewayRealtimeAuthorization? authorization,
        GatewayRealtimeState realtimeState,
        WindowsCanvasNodeState nodeState,
        bool systemExecutionAdvertised)
    {
        var operatorConnected = realtimeState == GatewayRealtimeState.Connected && authorization is not null;
        var granted = authorization?.Scopes ?? [];
        var missing = authorization?.MissingRequestedScopes ?? GatewayRealtimeClient.RequestedOperatorScopes;
        var nodeConnected = nodeState == WindowsCanvasNodeState.Connected;

        // A device stuck on stale narrow scopes (connected but missing baseline operator scopes) can re-request
        // access through the repair flow.
        var repairAvailable = operatorConnected && missing.Count > 0;

        var explanation = systemExecutionAdvertised
            ? "This Windows node advertises system execution commands, so approving its pairing needs an operator with operator.admin in addition to operator.pairing."
            : "Approving this Windows node's pairing needs an operator with operator.pairing.";

        return new WindowsNodeAccessStatus(
            Capability: authorization?.Capability ?? "unknown",
            GrantedScopes: granted,
            MissingScopes: missing,
            ScopeSummary: authorization?.ScopeSummary ?? "Operator channel not connected.",
            OperatorConnected: operatorConnected,
            NodeConnected: nodeConnected,
            RequiresAdminForNode: systemExecutionAdvertised,
            RepairAvailable: repairAvailable,
            NodeStateLabel: DescribeNodeState(nodeState),
            Explanation: explanation);
    }

    private static string DescribeNodeState(WindowsCanvasNodeState nodeState)
    {
        return nodeState switch
        {
            WindowsCanvasNodeState.Connected => "Connected and paired",
            WindowsCanvasNodeState.Connecting => "Connecting",
            WindowsCanvasNodeState.PairingRequired => "Pairing required",
            WindowsCanvasNodeState.AuthFailed => "Authentication failed",
            _ => "Disconnected",
        };
    }
}
