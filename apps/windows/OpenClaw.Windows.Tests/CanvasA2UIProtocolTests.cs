using OpenClaw.Windows;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class CanvasA2UIProtocolTests
{
    [TestMethod]
    public void CommandStringsAreStable()
    {
        Assert.AreEqual("canvas.a2ui.push", ConstantValue(nameof(WindowsCanvasA2UICommand.Push)));
        Assert.AreEqual("canvas.a2ui.pushJSONL", ConstantValue(nameof(WindowsCanvasA2UICommand.PushJsonl)));
        Assert.AreEqual("canvas.a2ui.reset", ConstantValue(nameof(WindowsCanvasA2UICommand.Reset)));
    }

    [TestMethod]
    public void ResolveA2UIUrl_AppendsWindowsPlatformHostToCanvasSurfaceUrl()
    {
        var url = WindowsCanvasA2UIUrl.ResolveFromCanvasPluginSurfaceUrl(
            " http://127.0.0.1:18789/__openclaw__/cap/token/ ");

        Assert.AreEqual(
            "http://127.0.0.1:18789/__openclaw__/cap/token/__openclaw__/a2ui/?platform=windows",
            url);
    }

    [TestMethod]
    public void ResolveA2UIUrl_ReturnsNullForMissingOrInvalidSurfaceUrl()
    {
        Assert.IsNull(WindowsCanvasA2UIUrl.ResolveFromCanvasPluginSurfaceUrl(null));
        Assert.IsNull(WindowsCanvasA2UIUrl.ResolveFromCanvasPluginSurfaceUrl(" "));
        Assert.IsNull(WindowsCanvasA2UIUrl.ResolveFromCanvasPluginSurfaceUrl("not a url"));
    }

    [TestMethod]
    public void ResolveA2UIHostUrl_UsesWindowsPlatformHelper()
    {
        var url = WindowsCanvasA2UI.ResolveA2UIHostUrl("https://node.example/__openclaw__/cap/cap_123");

        Assert.AreEqual(
            "https://node.example/__openclaw__/cap/cap_123/__openclaw__/a2ui/?platform=windows",
            url);
    }

    [TestMethod]
    public void InvokeResponseHelpersShapeSuccessAndFailureResults()
    {
        var success = WindowsCanvasInvokeResponse.Success("""{"ok":true}""");
        var failure = WindowsCanvasInvokeResponse.Failure("UNAVAILABLE", "Windows Canvas handler is not ready.");

        Assert.IsTrue(success.Ok);
        Assert.AreEqual("""{"ok":true}""", success.PayloadJson);
        Assert.IsNull(success.Error);
        Assert.IsFalse(failure.Ok);
        Assert.IsNull(failure.PayloadJson);
        Assert.IsNotNull(failure.Error);
        Assert.AreEqual("UNAVAILABLE", failure.Error.Code);
        Assert.AreEqual("Windows Canvas handler is not ready.", failure.Error.Message);
    }

    [TestMethod]
    public void ParseRendererResult_DoesNotRejectMissingOkFromOlderHosts()
    {
        var result = WindowsCanvasA2UI.ParseRendererResult("""{"hostPresent":true,"bodyText":"Canvas (A2UI)"}""");

        Assert.IsNotNull(result);
        Assert.IsNull(result.Ok);
        Assert.IsFalse(result.Rejected);
    }

    [TestMethod]
    public void ParseRendererResult_RejectsExplicitRendererFailure()
    {
        var result = WindowsCanvasA2UI.ParseRendererResult("""{"ok":false,"error":"missing openclawA2UI"}""");

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Ok);
        Assert.IsTrue(result.Rejected);
        Assert.AreEqual("missing openclawA2UI", result.Error);
    }

    [TestMethod]
    public void JsonlParserAcceptsV08MessageObjects()
    {
        var jsonl = """
            {"beginRendering":{"surfaceId":"main","timestamp":1}}
            {"surfaceUpdate":{"surfaceId":"main","ops":[]}}
            {"dataModelUpdate":{"dataModel":{"title":"Hello"}}}
            {"deleteSurface":{"surfaceId":"main"}}
            """;

        var messages = WindowsCanvasA2UIJsonl.DecodeMessagesFromJsonl(jsonl);

        Assert.HasCount(4, messages);
        Assert.IsTrue(messages[0].RootElement.TryGetProperty("beginRendering", out _));
        Assert.IsTrue(messages[1].RootElement.TryGetProperty("surfaceUpdate", out _));
        Assert.IsTrue(messages[2].RootElement.TryGetProperty("dataModelUpdate", out _));
        Assert.IsTrue(messages[3].RootElement.TryGetProperty("deleteSurface", out _));
    }

    [TestMethod]
    public void JsonlParserSkipsBlankLinesAndPreservesLineNumbersInErrors()
    {
        var jsonl = """

            {"beginRendering":{}}

            {"wat":{"nope":1}}
            """;

        var ex = Assert.ThrowsExactly<FormatException>(
            () => WindowsCanvasA2UIJsonl.DecodeMessagesFromJsonl(jsonl));

        StringAssert.Contains(ex.Message, "line 4");
    }

    [TestMethod]
    public void JsonlParserRejectsCreateSurface()
    {
        var jsonl = """
            {"createSurface":{"surfaceId":"main"}}
            """;

        var ex = Assert.ThrowsExactly<FormatException>(
            () => WindowsCanvasA2UIJsonl.DecodeMessagesFromJsonl(jsonl));

        StringAssert.Contains(ex.Message, "createSurface");
    }

    [TestMethod]
    public void JsonlParserRejectsMessagesWithoutExactlyOneV08MessageKey()
    {
        var missingKnownKey = Assert.ThrowsExactly<FormatException>(
            () => WindowsCanvasA2UIJsonl.DecodeMessagesFromJsonl("""{"wat":{"nope":1}}"""));
        StringAssert.Contains(missingKnownKey.Message, "expected exactly one");

        var multipleKnownKeys = Assert.ThrowsExactly<FormatException>(
            () => WindowsCanvasA2UIJsonl.DecodeMessagesFromJsonl(
                """{"beginRendering":{},"surfaceUpdate":{"surfaceId":"main","ops":[]}}"""));
        StringAssert.Contains(multipleKnownKeys.Message, "expected exactly one");
    }

    [TestMethod]
    public void JsonlParserRejectsNonObjectLines()
    {
        var ex = Assert.ThrowsExactly<FormatException>(
            () => WindowsCanvasA2UIJsonl.DecodeMessagesFromJsonl("""["not-object"]"""));

        StringAssert.Contains(ex.Message, "expected a JSON object");
    }

    private static string ConstantValue(string name)
    {
        var field = typeof(WindowsCanvasA2UICommand).GetField(name);
        Assert.IsNotNull(field);
        return field.GetRawConstantValue() as string
            ?? throw new AssertFailedException($"Expected {name} to be a string constant.");
    }
}
