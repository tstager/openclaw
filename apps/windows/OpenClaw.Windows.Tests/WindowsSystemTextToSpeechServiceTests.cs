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
}
