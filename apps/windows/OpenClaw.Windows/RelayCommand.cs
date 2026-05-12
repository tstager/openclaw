using System.Windows.Input;

namespace OpenClaw.Windows;

/// <summary>
/// Async ICommand implementation that disables repeated execution and reports exceptions centrally.
/// </summary>
public sealed class RelayCommand(Func<Task> execute, Action<Exception>? onError = null) : ICommand
{
    private readonly Func<Task> execute = execute;
    private readonly Action<Exception>? onError = onError;
    private bool isExecuting;

    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Prevents double-clicks from starting overlapping async UI actions.
    /// </summary>
    public bool CanExecute(object? parameter) => !this.isExecuting;

    public async void Execute(object? parameter)
    {
        if (!this.CanExecute(parameter))
        {
            return;
        }

        this.isExecuting = true;
        this.RaiseCanExecuteChanged();
        try
        {
            await this.execute();
        }
        catch (Exception ex)
        {
            this.onError?.Invoke(ex);
        }
        finally
        {
            this.isExecuting = false;
            this.RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
