using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsCompanionCommandFactoryTests
{
    [TestMethod]
    public async Task CreatedCommandRunsBeforeExecuteBeforeCommandBody()
    {
        var calls = new List<string>();
        var factory = new WindowsCompanionCommandFactory(
            () =>
            {
                calls.Add("before");
                return Task.CompletedTask;
            },
            _ => { });
        var command = factory.Create(() =>
        {
            calls.Add("execute");
            return Task.CompletedTask;
        });

        command.Execute(null);

        await WaitUntilAsync(() => command.CanExecute(null));
        CollectionAssert.AreEqual(new[] { "before", "execute" }, calls);
    }

    [TestMethod]
    public async Task CreatedCommandReportsExecutionErrors()
    {
        var errors = new List<Exception>();
        var expected = new InvalidOperationException("command failed");
        var factory = new WindowsCompanionCommandFactory(() => Task.CompletedTask, errors.Add);
        var command = factory.Create(() => Task.FromException(expected));

        command.Execute(null);

        await WaitUntilAsync(() => command.CanExecute(null));
        Assert.AreSame(expected, errors.Single());
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
