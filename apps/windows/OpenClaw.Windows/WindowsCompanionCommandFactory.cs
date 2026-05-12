namespace OpenClaw.Windows;

/// <summary>
/// Creates UI commands with shared pre-execution cleanup and error reporting behavior.
/// </summary>
public sealed class WindowsCompanionCommandFactory(
    Func<Task> beforeExecute,
    Action<Exception> onError)
{
    private readonly Func<Task> beforeExecute = beforeExecute;
    private readonly Action<Exception> onError = onError;

    /// <summary>
    /// Wraps a page action so stale command errors are cleared before the action starts.
    /// </summary>
    public RelayCommand Create(Func<Task> execute)
    {
        return new RelayCommand(async () =>
        {
            await this.beforeExecute();
            await execute();
        }, this.onError);
    }
}
