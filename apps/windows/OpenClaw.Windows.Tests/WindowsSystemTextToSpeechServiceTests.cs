using System.Runtime.InteropServices;
using OpenClaw.Windows.Native;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsSystemTextToSpeechServiceTests
{
    [TestMethod]
    public void StatusUsesInstalledVoices()
    {
        var service = new WindowsSystemTextToSpeechService(
            outputRoot: Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            runtime: new FakeWindowsTextToSpeechRuntime());

        var status = service.GetStatus();

        Assert.AreEqual("Available", status.State);
        Assert.AreEqual("Default Voice", status.DefaultVoice);
        Assert.AreEqual(2, status.InstalledVoiceCount);
    }

    [TestMethod]
    public async Task SynthesizeToFileAsyncWritesAudioForSelectedVoice()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var service = new WindowsSystemTextToSpeechService(outputRoot, new FakeWindowsTextToSpeechRuntime());

        var result = await service.SynthesizeToFileAsync(new WindowsTextToSpeechRequest("hello", "voice-2", "reply"));

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Path);
        Assert.IsTrue(File.Exists(result.Path));
        Assert.AreEqual("voice-2", result.Voice?.Id);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(result.Path));
    }

    [TestMethod]
    public void StatusReturnsUnavailableWhenWindowsSpeechComponentsAreMissing()
    {
        var service = new WindowsSystemTextToSpeechService(
            outputRoot: Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            runtime: new MissingWindowsSpeechComponentsRuntime());

        var status = service.GetStatus();

        Assert.AreEqual("Unavailable", status.State);
        Assert.AreEqual("Windows speech components are not installed on this device.", status.Detail);
        Assert.AreEqual(0, status.InstalledVoiceCount);
    }

    [TestMethod]
    public async Task SynthesizeToFileAsyncReturnsFailureWhenSpeechComponentsDisappear()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var service = new WindowsSystemTextToSpeechService(outputRoot, new SynthesisMissingWindowsSpeechComponentsRuntime());

        var result = await service.SynthesizeToFileAsync(new WindowsTextToSpeechRequest("hello", "voice-1", "reply"));

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("Windows speech components are not installed on this device.", result.Detail);
        Assert.IsNull(result.Path);
        Assert.AreEqual("voice-1", result.Voice?.Id);
    }

    private sealed class FakeWindowsTextToSpeechRuntime : IWindowsTextToSpeechRuntime
    {
        public IReadOnlyList<WindowsTextToSpeechVoice> GetInstalledVoices()
        {
            return
            [
                new WindowsTextToSpeechVoice("voice-1", "Default Voice", "en-US", "Female", true),
                new WindowsTextToSpeechVoice("voice-2", "Alt Voice", "en-GB", "Male", false),
            ];
        }

        public Task<WindowsTextToSpeechSynthesis> SynthesizeAsync(
            string text,
            string? voiceId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WindowsTextToSpeechSynthesis("audio/wav", [1, 2, 3, 4]));
        }
    }

    private sealed class MissingWindowsSpeechComponentsRuntime : IWindowsTextToSpeechRuntime
    {
        public IReadOnlyList<WindowsTextToSpeechVoice> GetInstalledVoices() =>
            throw new COMException("No installed components were detected.", unchecked((int)0x800F1000));

        public Task<WindowsTextToSpeechSynthesis> SynthesizeAsync(
            string text,
            string? voiceId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SynthesisMissingWindowsSpeechComponentsRuntime : IWindowsTextToSpeechRuntime
    {
        public IReadOnlyList<WindowsTextToSpeechVoice> GetInstalledVoices()
        {
            return
            [
                new WindowsTextToSpeechVoice("voice-1", "Default Voice", "en-US", "Female", true),
            ];
        }

        public Task<WindowsTextToSpeechSynthesis> SynthesizeAsync(
            string text,
            string? voiceId,
            CancellationToken cancellationToken = default) =>
            throw new COMException("No installed components were detected.", unchecked((int)0x800F1000));
    }
}
