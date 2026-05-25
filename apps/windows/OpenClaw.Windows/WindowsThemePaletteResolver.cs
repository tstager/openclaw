using Microsoft.UI.Xaml;
using Windows.UI;

namespace OpenClaw.Windows;

/// <summary>
/// Value palette for app-owned brushes so palette tests do not require the WinUI XAML runtime.
/// </summary>
public sealed record WindowsThemePalette(
    Color AppBackgroundColor,
    Color CardBackgroundColor,
    Color CardStrokeColor,
    Color LayerFillColor,
    Color TextPrimaryColor,
    Color TextSecondaryColor,
    Color AccentColor,
    Color AccentTextColor,
    Color SuccessColor,
    Color CautionColor,
    Color CriticalColor);

/// <summary>
/// Resolves app-owned surface and accent colors for the Windows companion shell.
/// </summary>
public static class WindowsThemePaletteResolver
{
    public static WindowsThemePalette Resolve(
        ElementTheme brightness,
        WindowsAccentColorPreference accentPreference,
        WindowsColorThemePreference colorThemePreference,
        Color? systemAccentColor = null,
        Color? customAccentColor = null,
        Color? customColorTheme = null)
    {
        var theme = brightness == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        var accent = ResolveAccentColor(accentPreference, theme, systemAccentColor, customAccentColor);
        return ResolveSurfacePalette(colorThemePreference, theme, customColorTheme) with
        {
            AccentColor = accent,
            AccentTextColor = ResolveTextOnAccentColor(accent),
        };
    }

    public static Color ResolveAccentColor(
        WindowsAccentColorPreference preference,
        ElementTheme brightness,
        Color? systemAccentColor = null,
        Color? customAccentColor = null)
    {
        var theme = brightness == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        if (preference == WindowsAccentColorPreference.System)
        {
            return systemAccentColor ?? ResolveSystemAccentFallback(theme);
        }

        if (preference == WindowsAccentColorPreference.Custom && customAccentColor is { } custom)
        {
            return Color.FromArgb(255, custom.R, custom.G, custom.B);
        }

        return preference switch
        {
            WindowsAccentColorPreference.Teal => theme == ElementTheme.Dark
                ? Color.FromArgb(255, 45, 212, 191)
                : Color.FromArgb(255, 13, 148, 136),
            WindowsAccentColorPreference.Green => theme == ElementTheme.Dark
                ? Color.FromArgb(255, 74, 222, 128)
                : Color.FromArgb(255, 22, 163, 74),
            WindowsAccentColorPreference.Orange => theme == ElementTheme.Dark
                ? Color.FromArgb(255, 251, 146, 60)
                : Color.FromArgb(255, 234, 88, 12),
            WindowsAccentColorPreference.Rose => theme == ElementTheme.Dark
                ? Color.FromArgb(255, 251, 113, 133)
                : Color.FromArgb(255, 225, 29, 72),
            WindowsAccentColorPreference.Purple => theme == ElementTheme.Dark
                ? Color.FromArgb(255, 168, 85, 247)
                : Color.FromArgb(255, 126, 34, 206),
            _ => theme == ElementTheme.Dark
                ? Color.FromArgb(255, 96, 165, 250)
                : Color.FromArgb(255, 37, 99, 235),
        };
    }

    public static Color ResolveTextOnAccentColor(Color accent)
    {
        return RelativeLuminance(accent) > 0.46
            ? Microsoft.UI.Colors.Black
            : Microsoft.UI.Colors.White;
    }

    private static WindowsThemePalette ResolveSurfacePalette(
        WindowsColorThemePreference preference,
        ElementTheme theme,
        Color? customColorTheme)
    {
        if (preference == WindowsColorThemePreference.Custom && customColorTheme is { } custom)
        {
            return CreateCustomPalette(Color.FromArgb(255, custom.R, custom.G, custom.B), theme);
        }

        return (preference, theme) switch
        {
            (WindowsColorThemePreference.Slate, ElementTheme.Dark) => CreatePalette(
                appBackground: Color.FromArgb(255, 2, 6, 23),
                cardBackground: Color.FromArgb(255, 15, 23, 42),
                cardStroke: Color.FromArgb(255, 51, 65, 85),
                layerFill: Color.FromArgb(255, 8, 15, 30),
                textPrimary: Color.FromArgb(255, 241, 245, 249),
                textSecondary: Color.FromArgb(255, 148, 163, 184),
                success: DefaultDark.SuccessColor,
                caution: DefaultDark.CautionColor,
                critical: DefaultDark.CriticalColor),
            (WindowsColorThemePreference.Slate, _) => CreatePalette(
                appBackground: Color.FromArgb(255, 241, 245, 249),
                cardBackground: Color.FromArgb(255, 248, 250, 252),
                cardStroke: Color.FromArgb(255, 203, 213, 225),
                layerFill: Color.FromArgb(255, 226, 232, 240),
                textPrimary: Color.FromArgb(255, 15, 23, 42),
                textSecondary: Color.FromArgb(255, 71, 85, 105),
                success: DefaultLight.SuccessColor,
                caution: DefaultLight.CautionColor,
                critical: DefaultLight.CriticalColor),
            (WindowsColorThemePreference.Forest, ElementTheme.Dark) => CreatePalette(
                appBackground: Color.FromArgb(255, 10, 25, 18),
                cardBackground: Color.FromArgb(255, 22, 35, 27),
                cardStroke: Color.FromArgb(255, 52, 78, 61),
                layerFill: Color.FromArgb(255, 15, 29, 22),
                textPrimary: Color.FromArgb(255, 232, 245, 236),
                textSecondary: Color.FromArgb(255, 167, 196, 173),
                success: DefaultDark.SuccessColor,
                caution: DefaultDark.CautionColor,
                critical: DefaultDark.CriticalColor),
            (WindowsColorThemePreference.Forest, _) => CreatePalette(
                appBackground: Color.FromArgb(255, 240, 247, 242),
                cardBackground: Color.FromArgb(255, 250, 252, 250),
                cardStroke: Color.FromArgb(255, 190, 210, 194),
                layerFill: Color.FromArgb(255, 230, 240, 232),
                textPrimary: Color.FromArgb(255, 20, 46, 32),
                textSecondary: Color.FromArgb(255, 74, 122, 85),
                success: DefaultLight.SuccessColor,
                caution: DefaultLight.CautionColor,
                critical: DefaultLight.CriticalColor),
            (WindowsColorThemePreference.Ocean, ElementTheme.Dark) => CreatePalette(
                appBackground: Color.FromArgb(255, 8, 23, 39),
                cardBackground: Color.FromArgb(255, 12, 37, 60),
                cardStroke: Color.FromArgb(255, 36, 99, 132),
                layerFill: Color.FromArgb(255, 10, 30, 50),
                textPrimary: Color.FromArgb(255, 224, 242, 254),
                textSecondary: Color.FromArgb(255, 147, 197, 253),
                success: DefaultDark.SuccessColor,
                caution: DefaultDark.CautionColor,
                critical: DefaultDark.CriticalColor),
            (WindowsColorThemePreference.Ocean, _) => CreatePalette(
                appBackground: Color.FromArgb(255, 239, 246, 255),
                cardBackground: Color.FromArgb(255, 247, 250, 252),
                cardStroke: Color.FromArgb(255, 186, 230, 253),
                layerFill: Color.FromArgb(255, 224, 242, 254),
                textPrimary: Color.FromArgb(255, 8, 47, 73),
                textSecondary: Color.FromArgb(255, 12, 92, 138),
                success: DefaultLight.SuccessColor,
                caution: DefaultLight.CautionColor,
                critical: DefaultLight.CriticalColor),
            (WindowsColorThemePreference.Ember, ElementTheme.Dark) => CreatePalette(
                appBackground: Color.FromArgb(255, 32, 16, 9),
                cardBackground: Color.FromArgb(255, 52, 24, 12),
                cardStroke: Color.FromArgb(255, 146, 64, 14),
                layerFill: Color.FromArgb(255, 40, 18, 9),
                textPrimary: Color.FromArgb(255, 255, 237, 213),
                textSecondary: Color.FromArgb(255, 253, 186, 116),
                success: DefaultDark.SuccessColor,
                caution: DefaultDark.CautionColor,
                critical: DefaultDark.CriticalColor),
            (WindowsColorThemePreference.Ember, _) => CreatePalette(
                appBackground: Color.FromArgb(255, 255, 247, 237),
                cardBackground: Color.FromArgb(255, 255, 251, 245),
                cardStroke: Color.FromArgb(255, 251, 191, 165),
                layerFill: Color.FromArgb(255, 255, 243, 224),
                textPrimary: Color.FromArgb(255, 124, 45, 18),
                textSecondary: Color.FromArgb(255, 154, 82, 40),
                success: DefaultLight.SuccessColor,
                caution: DefaultLight.CautionColor,
                critical: DefaultLight.CriticalColor),
            (WindowsColorThemePreference.HighContrast, ElementTheme.Dark) => CreatePalette(
                appBackground: Microsoft.UI.Colors.Black,
                cardBackground: Color.FromArgb(255, 15, 15, 15),
                cardStroke: Microsoft.UI.Colors.White,
                layerFill: Microsoft.UI.Colors.Black,
                textPrimary: Microsoft.UI.Colors.White,
                textSecondary: Color.FromArgb(255, 235, 235, 235),
                success: Color.FromArgb(255, 0, 255, 102),
                caution: Color.FromArgb(255, 255, 221, 0),
                critical: Color.FromArgb(255, 255, 99, 99)),
            (WindowsColorThemePreference.HighContrast, _) => CreatePalette(
                appBackground: Microsoft.UI.Colors.White,
                cardBackground: Microsoft.UI.Colors.White,
                cardStroke: Microsoft.UI.Colors.Black,
                layerFill: Color.FromArgb(255, 245, 245, 245),
                textPrimary: Microsoft.UI.Colors.Black,
                textSecondary: Color.FromArgb(255, 32, 32, 32),
                success: Color.FromArgb(255, 0, 92, 32),
                caution: Color.FromArgb(255, 120, 60, 0),
                critical: Color.FromArgb(255, 146, 0, 0)),
            (_, ElementTheme.Dark) => DefaultDark,
            _ => DefaultLight,
        };
    }

    private static WindowsThemePalette CreatePalette(
        Color appBackground,
        Color cardBackground,
        Color cardStroke,
        Color layerFill,
        Color textPrimary,
        Color textSecondary,
        Color success,
        Color caution,
        Color critical)
    {
        return new WindowsThemePalette(
            AppBackgroundColor: appBackground,
            CardBackgroundColor: cardBackground,
            CardStrokeColor: cardStroke,
            LayerFillColor: layerFill,
            TextPrimaryColor: textPrimary,
            TextSecondaryColor: textSecondary,
            AccentColor: ResolveAccentColor(WindowsAccentColorPreference.Blue, ElementTheme.Light),
            AccentTextColor: Microsoft.UI.Colors.White,
            SuccessColor: success,
            CautionColor: caution,
            CriticalColor: critical);
    }

    private static Color ResolveSystemAccentFallback(ElementTheme theme)
    {
        return theme == ElementTheme.Dark
            ? Color.FromArgb(255, 96, 165, 250)
            : Color.FromArgb(255, 37, 99, 235);
    }

    private static WindowsThemePalette CreateCustomPalette(Color seed, ElementTheme theme)
    {
        if (theme == ElementTheme.Dark)
        {
            return CreatePalette(
                appBackground: Scale(seed, 0.125),
                cardBackground: Scale(seed, 0.4),
                cardStroke: seed,
                layerFill: Scale(seed, 0.25),
                textPrimary: Blend(Microsoft.UI.Colors.White, seed, 0.08),
                textSecondary: Blend(Microsoft.UI.Colors.White, seed, 0.45),
                success: DefaultDark.SuccessColor,
                caution: DefaultDark.CautionColor,
                critical: DefaultDark.CriticalColor);
        }

        return CreatePalette(
            appBackground: Blend(Microsoft.UI.Colors.White, seed, 0.08),
            cardBackground: Blend(Microsoft.UI.Colors.White, seed, 0.04),
            cardStroke: Blend(Microsoft.UI.Colors.White, seed, 0.35),
            layerFill: Blend(Microsoft.UI.Colors.White, seed, 0.12),
            textPrimary: Scale(seed, 0.32),
            textSecondary: Scale(seed, 0.58),
            success: DefaultLight.SuccessColor,
            caution: DefaultLight.CautionColor,
            critical: DefaultLight.CriticalColor);
    }

    private static Color Scale(Color color, double factor)
    {
        return Color.FromArgb(
            255,
            ScaleChannel(color.R, factor),
            ScaleChannel(color.G, factor),
            ScaleChannel(color.B, factor));
    }

    private static Color Blend(Color baseColor, Color overlay, double overlayWeight)
    {
        return Color.FromArgb(
            255,
            BlendChannel(baseColor.R, overlay.R, overlayWeight),
            BlendChannel(baseColor.G, overlay.G, overlayWeight),
            BlendChannel(baseColor.B, overlay.B, overlayWeight));
    }

    private static byte ScaleChannel(byte value, double factor)
    {
        return (byte)Math.Clamp((int)Math.Round(value * factor, MidpointRounding.AwayFromZero), 0, 255);
    }

    private static byte BlendChannel(byte baseValue, byte overlayValue, double overlayWeight)
    {
        var baseWeight = 1.0 - overlayWeight;
        return (byte)Math.Clamp(
            (int)Math.Round(baseValue * baseWeight + overlayValue * overlayWeight, MidpointRounding.AwayFromZero),
            0,
            255);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255.0;
            return normalized <= 0.03928
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static readonly WindowsThemePalette DefaultDark = CreatePalette(
        appBackground: Color.FromArgb(255, 15, 15, 15),
        cardBackground: Color.FromArgb(255, 31, 31, 31),
        cardStroke: Color.FromArgb(255, 62, 62, 62),
        layerFill: Color.FromArgb(255, 24, 24, 24),
        textPrimary: Color.FromArgb(255, 255, 255, 255),
        textSecondary: Color.FromArgb(255, 196, 196, 196),
        success: Color.FromArgb(255, 108, 203, 95),
        caution: Color.FromArgb(255, 249, 199, 79),
        critical: Color.FromArgb(255, 255, 107, 107));

    private static readonly WindowsThemePalette DefaultLight = CreatePalette(
        appBackground: Color.FromArgb(255, 247, 247, 247),
        cardBackground: Color.FromArgb(255, 255, 255, 255),
        cardStroke: Color.FromArgb(255, 229, 231, 235),
        layerFill: Color.FromArgb(255, 248, 248, 248),
        textPrimary: Color.FromArgb(255, 26, 26, 26),
        textSecondary: Color.FromArgb(255, 96, 96, 96),
        success: Color.FromArgb(255, 24, 128, 56),
        caution: Color.FromArgb(255, 151, 104, 0),
        critical: Color.FromArgb(255, 185, 28, 28));
}
