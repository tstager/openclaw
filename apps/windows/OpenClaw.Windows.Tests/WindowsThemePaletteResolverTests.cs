using Microsoft.UI.Xaml;
using OpenClaw.Windows;
using Windows.UI;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsThemePaletteResolverTests
{
    [TestMethod]
    public void Resolve_DefaultPaletteMatchesSessionOneLightColors()
    {
        var palette = WindowsThemePaletteResolver.Resolve(
            ElementTheme.Light,
            WindowsAccentColorPreference.Blue,
            WindowsColorThemePreference.Default);

        AssertColor(Color.FromArgb(255, 247, 247, 247), palette.AppBackgroundColor);
        AssertColor(Color.FromArgb(255, 255, 255, 255), palette.CardBackgroundColor);
        AssertColor(Color.FromArgb(255, 229, 231, 235), palette.CardStrokeColor);
        AssertColor(Color.FromArgb(255, 248, 248, 248), palette.LayerFillColor);
        AssertColor(Color.FromArgb(255, 26, 26, 26), palette.TextPrimaryColor);
        AssertColor(Color.FromArgb(255, 96, 96, 96), palette.TextSecondaryColor);
        AssertColor(Color.FromArgb(255, 37, 99, 235), palette.AccentColor);
        AssertColor(Microsoft.UI.Colors.White, palette.AccentTextColor);
        AssertColor(Color.FromArgb(255, 24, 128, 56), palette.SuccessColor);
        AssertColor(Color.FromArgb(255, 151, 104, 0), palette.CautionColor);
        AssertColor(Color.FromArgb(255, 185, 28, 28), palette.CriticalColor);
    }

    [TestMethod]
    public void Resolve_DefaultPaletteMatchesSessionOneDarkColors()
    {
        var palette = WindowsThemePaletteResolver.Resolve(
            ElementTheme.Dark,
            WindowsAccentColorPreference.Blue,
            WindowsColorThemePreference.Default);

        AssertColor(Color.FromArgb(255, 15, 15, 15), palette.AppBackgroundColor);
        AssertColor(Color.FromArgb(255, 31, 31, 31), palette.CardBackgroundColor);
        AssertColor(Color.FromArgb(255, 62, 62, 62), palette.CardStrokeColor);
        AssertColor(Color.FromArgb(255, 24, 24, 24), palette.LayerFillColor);
        AssertColor(Color.FromArgb(255, 255, 255, 255), palette.TextPrimaryColor);
        AssertColor(Color.FromArgb(255, 196, 196, 196), palette.TextSecondaryColor);
        AssertColor(Color.FromArgb(255, 96, 165, 250), palette.AccentColor);
        AssertColor(Microsoft.UI.Colors.White, palette.AccentTextColor);
        AssertColor(Color.FromArgb(255, 108, 203, 95), palette.SuccessColor);
        AssertColor(Color.FromArgb(255, 249, 199, 79), palette.CautionColor);
        AssertColor(Color.FromArgb(255, 255, 107, 107), palette.CriticalColor);
    }

    [TestMethod]
    public void Resolve_CustomThemesReturnExpectedDarkSurfaces()
    {
        var expectations = new[]
        {
            new ThemeExpectation(
                WindowsColorThemePreference.Slate,
                AppBackground: Color.FromArgb(255, 2, 6, 23),
                CardBackground: Color.FromArgb(255, 15, 23, 42),
                CardStroke: Color.FromArgb(255, 51, 65, 85),
                TextPrimary: Color.FromArgb(255, 241, 245, 249)),
            new ThemeExpectation(
                WindowsColorThemePreference.Forest,
                AppBackground: Color.FromArgb(255, 10, 25, 18),
                CardBackground: Color.FromArgb(255, 22, 35, 27),
                CardStroke: Color.FromArgb(255, 52, 78, 61),
                TextPrimary: Color.FromArgb(255, 232, 245, 236)),
            new ThemeExpectation(
                WindowsColorThemePreference.Ocean,
                AppBackground: Color.FromArgb(255, 8, 23, 39),
                CardBackground: Color.FromArgb(255, 12, 37, 60),
                CardStroke: Color.FromArgb(255, 36, 99, 132),
                TextPrimary: Color.FromArgb(255, 224, 242, 254)),
            new ThemeExpectation(
                WindowsColorThemePreference.Ember,
                AppBackground: Color.FromArgb(255, 32, 16, 9),
                CardBackground: Color.FromArgb(255, 52, 24, 12),
                CardStroke: Color.FromArgb(255, 146, 64, 14),
                TextPrimary: Color.FromArgb(255, 255, 237, 213)),
            new ThemeExpectation(
                WindowsColorThemePreference.HighContrast,
                AppBackground: Microsoft.UI.Colors.Black,
                CardBackground: Color.FromArgb(255, 15, 15, 15),
                CardStroke: Microsoft.UI.Colors.White,
                TextPrimary: Microsoft.UI.Colors.White),
        };

        foreach (var expectation in expectations)
        {
            var palette = WindowsThemePaletteResolver.Resolve(
                ElementTheme.Dark,
                WindowsAccentColorPreference.Green,
                expectation.Preference);

            AssertColor(expectation.AppBackground, palette.AppBackgroundColor);
            AssertColor(expectation.CardBackground, palette.CardBackgroundColor);
            AssertColor(expectation.CardStroke, palette.CardStrokeColor);
            AssertColor(expectation.TextPrimary, palette.TextPrimaryColor);

            if (expectation.Preference == WindowsColorThemePreference.HighContrast)
            {
                AssertColor(Color.FromArgb(255, 0, 255, 102), palette.SuccessColor);
                AssertColor(Color.FromArgb(255, 255, 221, 0), palette.CautionColor);
                AssertColor(Color.FromArgb(255, 255, 99, 99), palette.CriticalColor);
                continue;
            }

            AssertColor(Color.FromArgb(255, 108, 203, 95), palette.SuccessColor);
            AssertColor(Color.FromArgb(255, 249, 199, 79), palette.CautionColor);
            AssertColor(Color.FromArgb(255, 255, 107, 107), palette.CriticalColor);
        }
    }

    [TestMethod]
    public void Resolve_SystemAccentUsesProvidedSystemAccent()
    {
        var providedSystemAccent = Color.FromArgb(255, 230, 180, 40);
        var palette = WindowsThemePaletteResolver.Resolve(
            ElementTheme.Light,
            WindowsAccentColorPreference.System,
            WindowsColorThemePreference.Ocean,
            providedSystemAccent);

        AssertColor(providedSystemAccent, palette.AccentColor);
        AssertColor(Microsoft.UI.Colors.Black, palette.AccentTextColor);
    }

    private static void AssertColor(Color expected, Color actual)
    {
        Assert.AreEqual(expected, actual);
    }

    private sealed record ThemeExpectation(
        WindowsColorThemePreference Preference,
        Color AppBackground,
        Color CardBackground,
        Color CardStroke,
        Color TextPrimary);
}
