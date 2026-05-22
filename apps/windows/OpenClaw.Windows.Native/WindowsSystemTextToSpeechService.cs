using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;

namespace OpenClaw.Windows.Native;

/// <summary>
/// One installed Windows speech synthesis voice.
/// </summary>
public sealed record WindowsTextToSpeechVoice(
    string Id,
    string DisplayName,
    string Language,
    string Gender,
    bool IsDefault);

/// <summary>
/// Request for local Windows text-to-speech synthesis.
/// </summary>
public sealed record WindowsTextToSpeechRequest(
    string Text,
    string? VoiceId = null,
    string Prefix = "tts");

/// <summary>
/// Status summary for the built-in Windows speech provider.
/// </summary>
public sealed record WindowsTextToSpeechStatus(
    string State,
    string Detail,
    string? DefaultVoice,
    int InstalledVoiceCount);

/// <summary>
/// Completed text-to-speech output written to local storage.
/// </summary>
public sealed record WindowsTextToSpeechResult(
    bool Succeeded,
    string? Path,
    string Detail,
    string ContentType,
    WindowsTextToSpeechVoice? Voice);

/// <summary>
/// Raw speech synthesis payload returned by a runtime backend.
/// </summary>
public sealed record WindowsTextToSpeechSynthesis(string ContentType, byte[] Audio);

/// <summary>
/// Abstraction over the Windows speech runtime so tests can validate file and voice handling.
/// </summary>
public interface IWindowsTextToSpeechRuntime
{
    IReadOnlyList<WindowsTextToSpeechVoice> GetInstalledVoices();

    Task<WindowsTextToSpeechSynthesis> SynthesizeAsync(
        string text,
        string? voiceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimal Windows system TTS provider that writes synthesized speech to local files.
/// </summary>
public sealed class WindowsSystemTextToSpeechService
{
    private readonly IWindowsTextToSpeechRuntime runtime;

    public WindowsSystemTextToSpeechService(
        string? outputRoot = null,
        IWindowsTextToSpeechRuntime? runtime = null)
    {
        this.OutputRoot = outputRoot ?? DefaultOutputRoot();
        this.runtime = runtime ?? new WinRtWindowsTextToSpeechRuntime();
    }

    public string OutputRoot { get; }

    /// <summary>
    /// Lists currently installed Windows voices without synthesizing any audio.
    /// </summary>
    public IReadOnlyList<WindowsTextToSpeechVoice> GetAvailableVoices()
    {
        return this.runtime.GetInstalledVoices();
    }

    /// <summary>
    /// Returns a display-ready provider status for capability surfaces and diagnostics.
    /// </summary>
    public WindowsTextToSpeechStatus GetStatus()
    {
        var voices = this.GetAvailableVoices();
        var defaultVoice = voices.FirstOrDefault(voice => voice.IsDefault) ?? voices.FirstOrDefault();
        return voices.Count == 0
            ? new WindowsTextToSpeechStatus(
                "Unavailable",
                "No installed Windows speech voices were detected.",
                null,
                0)
            : new WindowsTextToSpeechStatus(
                "Available",
                "Installed Windows voices can synthesize local reply audio clips.",
                defaultVoice?.DisplayName,
                voices.Count);
    }

    /// <summary>
    /// Synthesizes text with the selected Windows voice and writes the result to disk.
    /// </summary>
    public async Task<WindowsTextToSpeechResult> SynthesizeToFileAsync(
        WindowsTextToSpeechRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new ArgumentException("Speech text must not be empty.", nameof(request));
        }

        var voices = this.GetAvailableVoices();
        if (voices.Count == 0)
        {
            return new WindowsTextToSpeechResult(
                false,
                null,
                "No installed Windows speech voices are available.",
                "audio/wav",
                null);
        }

        var selectedVoice = ResolveVoice(voices, request.VoiceId);
        if (selectedVoice is null)
        {
            return new WindowsTextToSpeechResult(
                false,
                null,
                $"Windows voice '{request.VoiceId}' is not installed.",
                "audio/wav",
                null);
        }

        var synthesis = await this.runtime.SynthesizeAsync(request.Text, selectedVoice.Id, cancellationToken);
        var extension = FileExtensionFor(synthesis.ContentType);
        Directory.CreateDirectory(this.OutputRoot);
        var path = WindowsDeviceCapabilityService.CreateCapturePath(this.OutputRoot, request.Prefix, extension);
        await File.WriteAllBytesAsync(path, synthesis.Audio, cancellationToken);

        return new WindowsTextToSpeechResult(
            true,
            path,
            $"Saved synthesized speech to {path} using {selectedVoice.DisplayName}.",
            synthesis.ContentType,
            selectedVoice);
    }

    private static string DefaultOutputRoot()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "OpenClaw", "WindowsCompanion", "Speech");
    }

    private static WindowsTextToSpeechVoice? ResolveVoice(
        IReadOnlyList<WindowsTextToSpeechVoice> voices,
        string? requestedVoiceId)
    {
        if (string.IsNullOrWhiteSpace(requestedVoiceId))
        {
            return voices.FirstOrDefault(voice => voice.IsDefault) ?? voices.FirstOrDefault();
        }

        return voices.FirstOrDefault(voice => string.Equals(voice.Id, requestedVoiceId, StringComparison.Ordinal));
    }

    private static string FileExtensionFor(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "audio/mpeg" => "mp3",
            "audio/mp3" => "mp3",
            _ => "wav",
        };
    }

    private sealed class WinRtWindowsTextToSpeechRuntime : IWindowsTextToSpeechRuntime
    {
        public IReadOnlyList<WindowsTextToSpeechVoice> GetInstalledVoices()
        {
            using var synthesizer = new SpeechSynthesizer();
            var defaultVoiceId = synthesizer.Voice.Id;
            return SpeechSynthesizer.AllVoices
                .Select(voice => new WindowsTextToSpeechVoice(
                    voice.Id,
                    voice.DisplayName,
                    voice.Language,
                    voice.Gender.ToString(),
                    string.Equals(voice.Id, defaultVoiceId, StringComparison.Ordinal)))
                .ToArray();
        }

        public async Task<WindowsTextToSpeechSynthesis> SynthesizeAsync(
            string text,
            string? voiceId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var synthesizer = new SpeechSynthesizer();
            if (!string.IsNullOrWhiteSpace(voiceId))
            {
                var voice = SpeechSynthesizer.AllVoices.FirstOrDefault(
                    candidate => string.Equals(candidate.Id, voiceId, StringComparison.Ordinal));
                if (voice is not null)
                {
                    synthesizer.Voice = voice;
                }
            }

            using var stream = await synthesizer.SynthesizeTextToStreamAsync(text);
            cancellationToken.ThrowIfCancellationRequested();

            var length = checked((uint)stream.Size);
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync(length);
            var bytes = new byte[length];
            reader.ReadBytes(bytes);
            return new WindowsTextToSpeechSynthesis(stream.ContentType, bytes);
        }
    }
}
