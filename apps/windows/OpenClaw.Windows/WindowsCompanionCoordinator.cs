namespace OpenClaw.Windows;

public sealed class WindowsCompanionCoordinator(WindowsCompanionState appState)
{
    private readonly WindowsCompanionState appState = appState;

    public GatewayStatusSnapshot? GatewayStatus { get; private set; }

    public GatewayRealtimeState RealtimeState { get; private set; } = GatewayRealtimeState.Disconnected;

    public string? RealtimeReason { get; private set; }

    public IReadOnlyList<OnboardingCheckResult> OnboardingChecks { get; private set; } = [];

    public string? LogPath => this.GatewayStatus?.LogPath;

    public string? LastError { get; private set; }

    public string? LastActivity { get; private set; }

    public GatewayDashboardSummary DashboardSummary =>
        GatewayDashboardSummary.Create(
            this.GatewayStatus,
            this.RealtimeState,
            this.OnboardingChecks,
            this.appState.Realtime.Authorization);

    public async Task<GatewayStatusSnapshot> RefreshGatewayStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = await this.appState.Gateway.RefreshStatusAsync(cancellationToken);
        this.ApplyGatewayStatus(status);
        return status;
    }

    public async Task<IReadOnlyList<OnboardingCheckResult>> RefreshOnboardingAsync(
        CancellationToken cancellationToken = default)
    {
        this.OnboardingChecks = await this.appState.OnboardingChecks.RunAsync(cancellationToken);
        return this.OnboardingChecks;
    }

    public async Task<GatewayActionResult> RunGatewayActionAsync(
        GatewayCliAction action,
        CancellationToken cancellationToken = default)
    {
        this.LastActivity = $"{action} started.";
        var result = await this.appState.Gateway.RunActionAsync(action, cancellationToken);
        this.ApplyGatewayStatus(result.Status);
        this.LastActivity = result.Succeeded
            ? $"{action} completed."
            : $"{action} failed: {result.Output}";
        if (!result.Succeeded)
        {
            this.LastError = result.Output;
        }
        return result;
    }

    public void ApplyGatewayStatus(GatewayStatusSnapshot status)
    {
        this.GatewayStatus = status;
        this.LastError = status.Error;
    }

    public void ClearGatewayStatus(Exception exception)
    {
        this.GatewayStatus = null;
        this.LastError = exception.Message;
    }

    public void ApplyRealtimeState(GatewayRealtimeState state, string? reason)
    {
        this.RealtimeState = state;
        this.RealtimeReason = reason;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            this.LastError = reason;
        }
    }

    public void RecordRealtimeEvent(GatewayRealtimeEvent @event)
    {
        this.LastActivity = $"Latest event: {@event.Name}";
    }

    public void RecordRefreshFailure(Exception exception)
    {
        this.LastError = exception.Message;
        this.LastActivity = $"Startup refresh failed: {exception.Message}";
    }
}
