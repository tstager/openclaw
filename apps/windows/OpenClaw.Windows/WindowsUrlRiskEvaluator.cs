namespace OpenClaw.Windows;

/// <summary>
/// Result of validating a URL before the shell navigates or persists it.
/// </summary>
public sealed record WindowsUrlRiskEvaluation(
    bool Allowed,
    string? NormalizedUrl,
    string? Reason);

/// <summary>
/// Blocks unsafe local schemes and clearly reports why a URL is unsuitable for navigation.
/// </summary>
public sealed class WindowsUrlRiskEvaluator
{
    public WindowsUrlRiskEvaluation Evaluate(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return new WindowsUrlRiskEvaluation(false, null, "A URL is required.");
        }

        if (!Uri.TryCreate(candidate.Trim(), UriKind.Absolute, out var uri))
        {
            return new WindowsUrlRiskEvaluation(false, null, "The URL is not a valid absolute URI.");
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return new WindowsUrlRiskEvaluation(true, uri.AbsoluteUri, null);
        }

        return new WindowsUrlRiskEvaluation(
            false,
            uri.AbsoluteUri,
            $"{uri.Scheme} URLs are blocked by the Windows companion policy.");
    }
}
