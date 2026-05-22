using System.Text.RegularExpressions;

namespace OpenClaw.Windows;

/// <summary>
/// Redacts common token- and password-like values before they are persisted locally.
/// </summary>
public sealed partial class WindowsSecretRedactor
{
    [GeneratedRegex("(?i)(bearer\\s+)([A-Za-z0-9._\\-]+)")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex("(?i)(\"?(?:token|secret|password|authorization|deviceToken)\"?\\s*[:=]\\s*\"?)([^\"\\s,}]+)")]
    private static partial Regex KeyValueSecretRegex();

    [GeneratedRegex("(?i)(api[_-]?key\\s*[:=]\\s*\"?)([^\"\\s,}]+)")]
    private static partial Regex ApiKeyRegex();

    public string Redact(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var redacted = BearerTokenRegex().Replace(text, "$1[redacted]");
        redacted = KeyValueSecretRegex().Replace(redacted, "$1[redacted]");
        redacted = ApiKeyRegex().Replace(redacted, "$1[redacted]");
        return redacted;
    }
}
