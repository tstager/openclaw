using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class RelayCommandTests
{
    [TestMethod]
    public async Task ExecuteDisablesCommandUntilTaskCompletes()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new RelayCommand(() => release.Task);
        var canExecuteChanges = 0;
        command.CanExecuteChanged += (_, _) => canExecuteChanges++;

        command.Execute(null);

        Assert.IsFalse(command.CanExecute(null));
        Assert.AreEqual(1, canExecuteChanges);

        release.SetResult();
        await WaitUntilAsync(() => command.CanExecute(null));

        Assert.IsTrue(command.CanExecute(null));
        Assert.AreEqual(2, canExecuteChanges);
    }

    [TestMethod]
    public async Task ExecuteIgnoresConcurrentInvocation()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var command = new RelayCommand(() =>
        {
            calls++;
            return release.Task;
        });

        command.Execute(null);
        command.Execute(null);

        Assert.AreEqual(1, calls);

        release.SetResult();
        await WaitUntilAsync(() => command.CanExecute(null));
    }

    [TestMethod]
    public async Task ExecuteReportsErrorsAndResetsCanExecute()
    {
        var errors = new List<Exception>();
        var expected = new InvalidOperationException("command failed");
        var command = new RelayCommand(() => Task.FromException(expected), errors.Add);

        command.Execute(null);

        await WaitUntilAsync(() => command.CanExecute(null));

        Assert.AreSame(expected, errors.Single());
        Assert.IsTrue(command.CanExecute(null));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
