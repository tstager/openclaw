namespace OpenClaw.Windows;

/// <summary>
/// Keeps cross-page state synchronized between gateway CLI status, realtime events, and onboarding checks.
/// </summary>
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

    /// <summary>
    /// Aggregates the current coordinator state into the Home dashboard model.
    /// </summary>
    public GatewayDashboardSummary DashboardSummary =>
        GatewayDashboardSummary.Create(
            this.GatewayStatus,
            this.RealtimeState,
            this.OnboardingChecks,
            this.appState.Realtime.Authorization);

    /// <summary>
    /// Refreshes CLI gateway status and stores the result for all pages that display gateway state.
    /// </summary>
    public async Task<GatewayStatusSnapshot> RefreshGatewayStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = await this.appState.Gateway.RefreshStatusAsync(cancellationToken);
        this.ApplyGatewayStatus(status);
        return status;
    }

    /// <summary>
    /// Runs onboarding probes and caches their latest display results.
    /// </summary>
    public async Task<IReadOnlyList<OnboardingCheckResult>> RefreshOnboardingAsync(
        CancellationToken cancellationToken = default)
    {
        this.OnboardingChecks = await this.appState.OnboardingChecks.RunAsync(cancellationToken);
        return this.OnboardingChecks;
    }

    /// <summary>
    /// Executes a gateway lifecycle command and records the user-facing activity/error text.
    /// </summary>
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

    /// <summary>
    /// Applies a status snapshot received from either refresh or an action follow-up.
    /// </summary>
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

    /// <summary>
    /// Stores realtime connection state separately from CLI status because either channel can fail independently.
    /// </summary>
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
