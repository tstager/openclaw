using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsNodeCommandRegistryTests
{
    private static WindowsCanvasInvokeRequest Request(string command, string? paramsJson = null)
    {
        return new WindowsCanvasInvokeRequest("invoke-1", command, paramsJson, "node-1", 5000);
    }

    [TestMethod]
    public async Task InvokeAsyncDispatchesToRegisteredHandler()
    {
        var registry = new WindowsNodeCommandRegistry();
        registry.Register("canvas.present", (request, _) =>
            Task.FromResult(WindowsCanvasInvokeResponse.Success($$"""{"id":"{{request.Id}}"}""")));

        var response = await registry.InvokeAsync(Request("canvas.present"), CancellationToken.None);

        Assert.IsTrue(response.Ok);
        Assert.AreEqual("""{"id":"invoke-1"}""", response.PayloadJson);
    }

    [TestMethod]
    public async Task InvokeAsyncReturnsInvalidRequestForUnknownCommand()
    {
        var registry = new WindowsNodeCommandRegistry();
        registry.Register("canvas.present", (_, _) => Task.FromResult(WindowsCanvasInvokeResponse.Success()));

        var response = await registry.InvokeAsync(Request("camera.snap"), CancellationToken.None);

        Assert.IsFalse(response.Ok);
        Assert.IsNotNull(response.Error);
        Assert.AreEqual("INVALID_REQUEST", response.Error.Code);
        StringAssert.Contains(response.Error.Message, "camera.snap");
    }

    [TestMethod]
    public async Task DeclaredCommandWithoutHandlerIsAdvertisedAndReturnsUnavailable()
    {
        var registry = new WindowsNodeCommandRegistry();
        registry.Register("canvas.present", (_, _) => Task.FromResult(WindowsCanvasInvokeResponse.Success()));
        registry.DeclareCommand("screen.snapshot");

        CollectionAssert.AreEqual(new[] { "canvas.present", "screen.snapshot" }, registry.Commands.ToArray());

        var response = await registry.InvokeAsync(Request("screen.snapshot"), CancellationToken.None);

        Assert.IsFalse(response.Ok);
        Assert.IsNotNull(response.Error);
        Assert.AreEqual("UNAVAILABLE", response.Error.Code);
        StringAssert.Contains(response.Error.Message, "screen.snapshot");
    }

    [TestMethod]
    public void RegisteringAPreviouslyDeclaredCommandKeepsItsAdvertisedPosition()
    {
        var registry = new WindowsNodeCommandRegistry();
        registry.DeclareCommand("screen.snapshot");
        registry.DeclareCommand("camera.snap");
        registry.Register("screen.snapshot", (_, _) => Task.FromResult(WindowsCanvasInvokeResponse.Success()));

        CollectionAssert.AreEqual(new[] { "screen.snapshot", "camera.snap" }, registry.Commands.ToArray());
        Assert.IsTrue(registry.Contains("screen.snapshot"));
        Assert.IsFalse(registry.Contains("camera.snap"));
    }

    [TestMethod]
    public void CommandsPreserveRegistrationOrderAndDeduplicateOnReplace()
    {
        var registry = new WindowsNodeCommandRegistry();
        registry.Register("canvas.present", (_, _) => Task.FromResult(WindowsCanvasInvokeResponse.Success()));
        registry.Register("canvas.hide", (_, _) => Task.FromResult(WindowsCanvasInvokeResponse.Success()));
        registry.Register("canvas.present", (_, _) => Task.FromResult(WindowsCanvasInvokeResponse.Success("replaced")));

        CollectionAssert.AreEqual(new[] { "canvas.present", "canvas.hide" }, registry.Commands.ToArray());
        Assert.IsTrue(registry.Contains("canvas.hide"));
        Assert.IsFalse(registry.Contains("camera.snap"));
    }

    [TestMethod]
    public void CapabilitiesDeduplicateAndPermissionsProjectClaims()
    {
        var registry = new WindowsNodeCommandRegistry();
        registry.DeclareCapability("canvas");
        registry.DeclareCapability("canvas");
        registry.DeclareCapability("screen");
        registry.DeclarePermission("canvas.a2ui", true);
        registry.DeclarePermission("screen.record", false);
        registry.DeclarePermission("screen.record", true);

        CollectionAssert.AreEqual(new[] { "canvas", "screen" }, registry.Capabilities.ToArray());
        Assert.HasCount(2, registry.Permissions);
        Assert.IsTrue((bool)registry.Permissions["canvas.a2ui"]!);
        Assert.IsTrue((bool)registry.Permissions["screen.record"]!);
    }
}
