using System.Text.Json;
using OpenClaw.Protocol.Generated;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class GatewayFrameReaderTests
{
    [TestMethod]
    public void DeserializesRequestFrame()
    {
        var frame = GatewayFrameReader.Deserialize(
            """{"type":"req","id":"1","method":"gateway.status","params":{"deep":true}}""");

        Assert.AreEqual("req", frame.Type);
        Assert.IsNotNull(frame.Request);
        Assert.AreEqual("1", frame.Request.Id);
        Assert.AreEqual("gateway.status", frame.Request.Method);
        Assert.AreEqual(JsonValueKind.Object, frame.Request.Params?.ValueKind);
    }

    [TestMethod]
    public void DeserializesResponseFrame()
    {
        var frame = GatewayFrameReader.Deserialize(
            """{"type":"res","id":"1","ok":false,"error":{"code":"UNAVAILABLE","message":"Gateway unavailable","retryable":true}}""");

        Assert.AreEqual("res", frame.Type);
        Assert.IsNotNull(frame.Response);
        Assert.IsFalse(frame.Response.Ok);
        Assert.AreEqual(ErrorCodes.Unavailable, frame.Response.Error?.Code);
        Assert.IsTrue(frame.Response.Error?.Retryable);
    }

    [TestMethod]
    public void DeserializesEventFrame()
    {
        var frame = GatewayFrameReader.Deserialize(
            """{"type":"event","event":"tick","payload":{"ts":1},"seq":42}""");

        Assert.AreEqual("event", frame.Type);
        Assert.IsNotNull(frame.Event);
        Assert.AreEqual("tick", frame.Event.Event);
        Assert.AreEqual(42, frame.Event.Seq);
    }
}
