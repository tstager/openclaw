using System.Windows.Input;

namespace OpenClaw.Windows;

public sealed class RelayCommand(Func<Task> execute) : ICommand
{
    private readonly Func<Task> execute = execute;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter)
    {
        await this.execute();
    }

    public void RaiseCanExecuteChanged()
    {
        this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
