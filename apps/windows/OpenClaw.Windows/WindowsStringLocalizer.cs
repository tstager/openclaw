using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Windows.ApplicationModel.Resources;

namespace OpenClaw.Windows;

/// <summary>
/// Looks up MRT-backed shell strings with fallback values for the programmatic WinUI surface.
/// </summary>
public interface IWindowsStringLocalizer
{
    string Get(string resourceKey, string fallback);

    string Format(string resourceKey, string fallbackFormat, params object[] arguments);
}

/// <summary>
/// Resource-backed localizer for Windows companion shell text.
/// </summary>
public sealed class WindowsStringLocalizer : IWindowsStringLocalizer
{
    private const string ResourceFileName = "Resources";
    private readonly Func<string, string?> resourceLookup;

    public WindowsStringLocalizer()
        : this(CreateDefaultLookup())
    {
    }

    public WindowsStringLocalizer(Func<string, string?> resourceLookup)
    {
        this.resourceLookup = resourceLookup ?? throw new ArgumentNullException(nameof(resourceLookup));
    }

    public string Get(string resourceKey, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        ArgumentNullException.ThrowIfNull(fallback);

        var localized = this.resourceLookup(resourceKey);
        return string.IsNullOrWhiteSpace(localized)
            ? fallback
            : localized;
    }

    public string Format(string resourceKey, string fallbackFormat, params object[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        ArgumentNullException.ThrowIfNull(fallbackFormat);
        ArgumentNullException.ThrowIfNull(arguments);

        return string.Format(
            CultureInfo.CurrentCulture,
            this.Get(resourceKey, fallbackFormat),
            arguments);
    }

    private static Func<string, string?> CreateDefaultLookup()
    {
        Lazy<ResourceManager?> resourceManager = new(() =>
        {
            try
            {
                return new ResourceManager();
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or COMException)
            {
                return null;
            }
        });

        return resourceKey =>
        {
            try
            {
                var candidate = resourceManager.Value?.MainResourceMap.GetValue($"{ResourceFileName}/{resourceKey}");
                var value = candidate?.ValueAsString;
                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : value;
            }
            catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidOperationException or COMException)
            {
                return null;
            }
        };
    }
}
