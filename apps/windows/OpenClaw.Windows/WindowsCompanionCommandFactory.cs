namespace OpenClaw.Windows;

public sealed class WindowsCompanionCommandFactory(
    Func<Task> beforeExecute,
    Action<Exception> onError)
{
    private readonly Func<Task> beforeExecute = beforeExecute;
    private readonly Action<Exception> onError = onError;

    public RelayCommand Create(Func<Task> execute)
    {
        return new RelayCommand(async () =>
        {
            await this.beforeExecute();
            await execute();
        }, this.onError);
    }
}
