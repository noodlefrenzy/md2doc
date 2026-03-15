// agent-notes: { ctx: "Red-phase tests for MermaidThemeConfig DTO, cache key, contrast", deps: [Md2.Diagrams.MermaidThemeConfig, Md2.Core.Pipeline.ResolvedTheme, Shouldly], state: red, last: "tara@2026-03-13" }

using Md2.Core.Pipeline;
using Shouldly;

namespace Md2.Diagrams.Tests;

public class MermaidThemeConfigTests
{
    // -----------------------------------------------------------------------
    // FromResolvedTheme mapping
    // -----------------------------------------------------------------------

    [Fact]
    public void FromResolvedTheme_MapsPrimaryColor()
    {
        var theme = new ResolvedTheme { PrimaryColor = "FF0000" };

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        config.PrimaryColor.ShouldBe("FF0000");
    }

    [Fact]
    public void FromResolvedTheme_MapsSecondaryColor()
    {
        var theme = new ResolvedTheme { SecondaryColor = "00FF00" };

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        config.SecondaryColor.ShouldBe("00FF00");
    }

    [Fact]
    public void FromResolvedTheme_MapsBodyTextColorToTextColor()
    {
        var theme = new ResolvedTheme { BodyTextColor = "111111" };

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        config.TextColor.ShouldBe("111111");
    }

    [Fact]
    public void FromResolvedTheme_DerivesPrimaryTextColorFromPrimaryForContrast()
    {
        // PrimaryTextColor should be derived for contrast against PrimaryColor (node background),
        // not taken from TableHeaderForeground which is unrelated to diagram rendering.
        var theme = new ResolvedTheme { PrimaryColor = "1B3A5C" }; // dark

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        // Dark primary → light text
        var r = Convert.ToInt32(config.PrimaryTextColor[..2], 16);
        r.ShouldBeGreaterThan(180);
    }

    [Fact]
    public void FromResolvedTheme_HacktermColors_DoesNotProduceGreenOnGreen()
    {
        // Hackterm: primary=00CC33 (green), tableHeaderForeground=00CC33 (green).
        // Previously this produced green text on green node backgrounds.
        var theme = new ResolvedTheme
        {
            PrimaryColor = "00CC33",
            TableHeaderForeground = "00CC33",
        };

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        // Node text should NOT be the same as the node background
        config.PrimaryTextColor.ShouldNotBe(config.PrimaryColor,
            "Mermaid node text must contrast with node background");
    }

    [Fact]
    public void FromResolvedTheme_MapsCodeBackgroundColorToBackgroundColor()
    {
        var theme = new ResolvedTheme { CodeBackgroundColor = "EEEEEE" };

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        config.BackgroundColor.ShouldBe("EEEEEE");
    }

    [Fact]
    public void FromResolvedTheme_MapsCodeBlockBorderColorToBorderColor()
    {
        var theme = new ResolvedTheme { CodeBlockBorderColor = "CCCCCC" };

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        config.BorderColor.ShouldBe("CCCCCC");
    }

    [Fact]
    public void FromResolvedTheme_MapsTableAlternateRowBackgroundToClusterBackground()
    {
        var theme = new ResolvedTheme { TableAlternateRowBackground = "FAFAFA" };

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        config.ClusterBackground.ShouldBe("FAFAFA");
    }

    [Fact]
    public void FromResolvedTheme_MapsHeadingFontToFontFamilyWithSansSerifFallback()
    {
        var theme = new ResolvedTheme { HeadingFont = "Georgia" };

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        config.FontFamily.ShouldContain("Georgia");
        config.FontFamily.ShouldContain("sans-serif");
    }

    [Fact]
    public void FromResolvedTheme_MapsBaseFontSizeFromPointsToPixels()
    {
        // 12pt * (96/72) = 16px
        var theme = new ResolvedTheme { BaseFontSize = 12.0 };

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        config.FontSizePx.ShouldBe(16.0, tolerance: 0.1);
    }

    [Fact]
    public void FromResolvedTheme_DefaultTheme_ProducesValidConfig()
    {
        var theme = ResolvedTheme.CreateDefault();

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        config.PrimaryColor.ShouldNotBeNullOrWhiteSpace();
        config.FontFamily.ShouldNotBeNullOrWhiteSpace();
        config.FontSizePx.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void FromResolvedTheme_PreservesHexWithoutHash()
    {
        // ResolvedTheme stores hex without #; MermaidThemeConfig should too
        var theme = new ResolvedTheme { PrimaryColor = "1B3A5C" };

        var config = MermaidThemeConfig.FromResolvedTheme(theme);

        config.PrimaryColor.ShouldNotStartWith("#");
    }

    // -----------------------------------------------------------------------
    // ToCacheKey determinism and differentiation
    // -----------------------------------------------------------------------

    [Fact]
    public void ToCacheKey_IsDeterministic_SameInputSameOutput()
    {
        var config = new MermaidThemeConfig
        {
            PrimaryColor = "1B3A5C",
            SecondaryColor = "4A90D9",
            TextColor = "333333",
            FontFamily = "Calibri, sans-serif",
            FontSizePx = 14.67
        };

        var key1 = config.ToCacheKey();
        var key2 = config.ToCacheKey();

        key1.ShouldBe(key2);
    }

    [Fact]
    public void ToCacheKey_DiffersWhenPrimaryColorChanges()
    {
        var config1 = new MermaidThemeConfig { PrimaryColor = "FF0000" };
        var config2 = new MermaidThemeConfig { PrimaryColor = "00FF00" };

        config1.ToCacheKey().ShouldNotBe(config2.ToCacheKey());
    }

    [Fact]
    public void ToCacheKey_DiffersWhenFontFamilyChanges()
    {
        var config1 = new MermaidThemeConfig { FontFamily = "Calibri, sans-serif" };
        var config2 = new MermaidThemeConfig { FontFamily = "Georgia, sans-serif" };

        config1.ToCacheKey().ShouldNotBe(config2.ToCacheKey());
    }

    [Fact]
    public void ToCacheKey_DiffersWhenFontSizeChanges()
    {
        var config1 = new MermaidThemeConfig { FontSizePx = 14.67 };
        var config2 = new MermaidThemeConfig { FontSizePx = 16.0 };

        config1.ToCacheKey().ShouldNotBe(config2.ToCacheKey());
    }

    [Fact]
    public void ToCacheKey_DiffersWhenSecondaryColorChanges()
    {
        var config1 = new MermaidThemeConfig { SecondaryColor = "4A90D9" };
        var config2 = new MermaidThemeConfig { SecondaryColor = "FF5733" };

        config1.ToCacheKey().ShouldNotBe(config2.ToCacheKey());
    }

    [Fact]
    public void ToCacheKey_DiffersWhenTextColorChanges()
    {
        var config1 = new MermaidThemeConfig { TextColor = "333333" };
        var config2 = new MermaidThemeConfig { TextColor = "FFFFFF" };

        config1.ToCacheKey().ShouldNotBe(config2.ToCacheKey());
    }

    [Fact]
    public void ToCacheKey_DiffersWhenBorderColorChanges()
    {
        var config1 = new MermaidThemeConfig { BorderColor = "E0E0E0" };
        var config2 = new MermaidThemeConfig { BorderColor = "000000" };

        config1.ToCacheKey().ShouldNotBe(config2.ToCacheKey());
    }

    [Fact]
    public void ToCacheKey_DiffersWhenBackgroundColorChanges()
    {
        var config1 = new MermaidThemeConfig { BackgroundColor = "F5F5F5" };
        var config2 = new MermaidThemeConfig { BackgroundColor = "000000" };

        config1.ToCacheKey().ShouldNotBe(config2.ToCacheKey());
    }

    [Fact]
    public void ToCacheKey_DiffersWhenPrimaryTextColorChanges()
    {
        var config1 = new MermaidThemeConfig { PrimaryTextColor = "FFFFFF" };
        var config2 = new MermaidThemeConfig { PrimaryTextColor = "000000" };

        config1.ToCacheKey().ShouldNotBe(config2.ToCacheKey());
    }

    [Fact]
    public void ToCacheKey_ReturnsNonEmptyString()
    {
        var config = new MermaidThemeConfig();

        var key = config.ToCacheKey();

        key.ShouldNotBeNullOrWhiteSpace();
    }

    // -----------------------------------------------------------------------
    // Contrast auto-derivation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("000000")] // Black
    [InlineData("1B3A5C")] // Dark navy (default primary)
    [InlineData("222222")] // Very dark grey
    [InlineData("0A3D0A")] // Dark green
    public void DeriveContrastTextColor_DarkBackground_ReturnsLightColor(string darkHex)
    {
        var result = MermaidThemeConfig.DeriveContrastTextColor(darkHex);

        // Light text color should have high luminance (we check the first two hex chars represent a high value)
        var r = Convert.ToInt32(result[..2], 16);
        var g = Convert.ToInt32(result[2..4], 16);
        var b = Convert.ToInt32(result[4..6], 16);
        var avgChannel = (r + g + b) / 3.0;

        avgChannel.ShouldBeGreaterThan(180,
            $"Dark background #{darkHex} should derive a light text color, got #{result}");
    }

    [Theory]
    [InlineData("FFFFFF")] // White
    [InlineData("F5F5F5")] // Light grey
    [InlineData("FFFF00")] // Yellow (high luminance)
    [InlineData("E0FFE0")] // Light green
    public void DeriveContrastTextColor_LightBackground_ReturnsDarkColor(string lightHex)
    {
        var result = MermaidThemeConfig.DeriveContrastTextColor(lightHex);

        var r = Convert.ToInt32(result[..2], 16);
        var g = Convert.ToInt32(result[2..4], 16);
        var b = Convert.ToInt32(result[4..6], 16);
        var avgChannel = (r + g + b) / 3.0;

        avgChannel.ShouldBeLessThan(100,
            $"Light background #{lightHex} should derive a dark text color, got #{result}");
    }

    [Fact]
    public void DeriveContrastTextColor_ReturnsValidSixCharHex()
    {
        var result = MermaidThemeConfig.DeriveContrastTextColor("1B3A5C");

        result.Length.ShouldBe(6);
        result.ShouldMatch("^[0-9A-Fa-f]{6}$");
    }
}
