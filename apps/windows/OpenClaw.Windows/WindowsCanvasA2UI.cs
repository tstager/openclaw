using System.Text.Json;

namespace OpenClaw.Windows;

/// <summary>
/// Stable command strings for Windows canvas invocation.
/// </summary>
public static class WindowsCanvasCommands
{
    public const string Present = "canvas.present";
    public const string Hide = "canvas.hide";
    public const string Navigate = "canvas.navigate";
    public const string Eval = "canvas.eval";
    public const string Snapshot = "canvas.snapshot";
    public const string A2UIPush = "canvas.a2ui.push";
    public const string A2UIPushJsonl = "canvas.a2ui.pushJSONL";
    public const string A2UIReset = "canvas.a2ui.reset";

    public static IReadOnlyList<string> All { get; } =
    [
        Present,
        Hide,
        Navigate,
        Eval,
        Snapshot,
        A2UIPush,
        A2UIPushJsonl,
        A2UIReset,
    ];
}

/// <summary>
/// Stable command strings for Windows canvas A2UI invocation.
/// </summary>
public static class WindowsCanvasA2UICommand
{
    public const string Push = WindowsCanvasCommands.A2UIPush;
    public const string PushJsonl = WindowsCanvasCommands.A2UIPushJsonl;
    public const string Reset = WindowsCanvasCommands.A2UIReset;
}

public sealed record WindowsCanvasInvokeRequest(
    string Id,
    string Command,
    string? ParamsJson,
    string? NodeId);

public sealed record WindowsCanvasInvokeError(string Code, string Message);

public sealed record WindowsCanvasInvokeResponse(
    bool Ok,
    string? PayloadJson,
    WindowsCanvasInvokeError? Error)
{
    public static WindowsCanvasInvokeResponse Success(string? payloadJson = null)
    {
        return new WindowsCanvasInvokeResponse(true, payloadJson, null);
    }

    public static WindowsCanvasInvokeResponse Failure(string code, string message)
    {
        return new WindowsCanvasInvokeResponse(false, null, new WindowsCanvasInvokeError(code, message));
    }
}

public static class WindowsCanvasA2UI
{
    public static string? ResolveA2UIHostUrl(string? canvasPluginSurfaceUrl)
    {
        return WindowsCanvasA2UIUrl.ResolveFromCanvasPluginSurfaceUrl(canvasPluginSurfaceUrl);
    }

    public static string DecodeExecuteScriptResult(string rawResult)
    {
        if (string.IsNullOrWhiteSpace(rawResult))
        {
            return "";
        }

        try
        {
            return JsonSerializer.Deserialize<string>(rawResult) ?? rawResult;
        }
        catch (JsonException)
        {
            return rawResult;
        }
    }
}

/// <summary>
/// Resolves Gateway canvas surface URLs to the Windows A2UI host URL.
/// </summary>
public static class WindowsCanvasA2UIUrl
{
    public static string? ResolveFromCanvasPluginSurfaceUrl(string? canvasPluginSurfaceUrl)
    {
        var trimmed = canvasPluginSurfaceUrl?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out _))
        {
            return null;
        }

        return $"{trimmed.TrimEnd('/')}/__openclaw__/a2ui/?platform=windows";
    }

    public static bool IsTrustedA2UIUrl(string? candidate, string? trustedA2UIUrl)
    {
        if (string.IsNullOrWhiteSpace(candidate) ||
            string.IsNullOrWhiteSpace(trustedA2UIUrl) ||
            !Uri.TryCreate(candidate, UriKind.Absolute, out var candidateUri) ||
            !Uri.TryCreate(trustedA2UIUrl, UriKind.Absolute, out var trustedUri))
        {
            return false;
        }

        return string.Equals(candidateUri.Scheme, trustedUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(candidateUri.Host, trustedUri.Host, StringComparison.OrdinalIgnoreCase) &&
               candidateUri.Port == trustedUri.Port &&
               string.Equals(
                   candidateUri.AbsolutePath.TrimEnd('/'),
                   trustedUri.AbsolutePath.TrimEnd('/'),
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(candidateUri.Query, trustedUri.Query, StringComparison.Ordinal);
    }
}

public sealed record WindowsCanvasA2UIJsonlMessage(int LineNumber, JsonElement RootElement);

/// <summary>
/// Parses and validates A2UI v0.8 server-to-client JSONL messages.
/// </summary>
public static class WindowsCanvasA2UIJsonl
{
    private static readonly HashSet<string> AllowedV08MessageKeys =
    [
        "beginRendering",
        "surfaceUpdate",
        "dataModelUpdate",
        "deleteSurface",
    ];

    public static IReadOnlyList<WindowsCanvasA2UIJsonlMessage> DecodeMessagesFromJsonl(string jsonl)
    {
        var messages = new List<WindowsCanvasA2UIJsonlMessage>();
        var lineNumber = 0;

        using var reader = new StringReader(jsonl);
        string? rawLine;
        while ((rawLine = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            using var document = ParseLine(line, lineNumber);
            ValidateV08Message(document.RootElement, lineNumber);
            messages.Add(new WindowsCanvasA2UIJsonlMessage(lineNumber, document.RootElement.Clone()));
        }

        return messages;
    }

    private static JsonDocument ParseLine(string line, int lineNumber)
    {
        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"A2UI JSONL line {lineNumber}: invalid JSON.", ex);
        }
    }

    private static void ValidateV08Message(JsonElement message, int lineNumber)
    {
        if (message.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"A2UI JSONL line {lineNumber}: expected a JSON object.");
        }

        var keys = message.EnumerateObject().Select(property => property.Name).ToArray();
        if (keys.Contains("createSurface", StringComparer.Ordinal))
        {
            throw new FormatException(
                $"A2UI JSONL line {lineNumber}: looks like A2UI v0.9 (`createSurface`). Canvas supports v0.8 messages only.");
        }

        var matched = keys.Where(AllowedV08MessageKeys.Contains).ToArray();
        if (matched.Length != 1)
        {
            var expected = string.Join(", ", AllowedV08MessageKeys.OrderBy(key => key, StringComparer.Ordinal));
            var found = keys.Length == 0 ? "(none)" : string.Join(", ", keys.OrderBy(key => key, StringComparer.Ordinal));
            throw new FormatException(
                $"A2UI JSONL line {lineNumber}: expected exactly one of {expected}; found: {found}.");
        }
    }
}
