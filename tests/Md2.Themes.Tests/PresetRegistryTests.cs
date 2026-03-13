// agent-notes: { ctx: "TDD tests for PresetRegistry — all 5 presets", deps: [src/Md2.Themes/PresetRegistry.cs], state: active, last: "sato@2026-03-12" }

using Shouldly;
using Md2.Themes;

namespace Md2.Themes.Tests;

public class PresetRegistryTests
{
    [Fact]
    public void ListPresets_ReturnsDefaultPreset()
    {
        var names = PresetRegistry.ListPresets();
        names.ShouldContain("default");
    }

    [Fact]
    public void GetPreset_Default_ReturnsValidTheme()
    {
        var theme = PresetRegistry.GetPreset("default");

        theme.ShouldNotBeNull();
        theme.Meta.ShouldNotBeNull();
        theme.Meta!.Name.ShouldBe("default");
        theme.Typography.ShouldNotBeNull();
        theme.Typography!.HeadingFont.ShouldNotBeNullOrWhiteSpace();
        theme.Typography.BodyFont.ShouldNotBeNullOrWhiteSpace();
        theme.Typography.MonoFont.ShouldNotBeNullOrWhiteSpace();
        theme.Colors.ShouldNotBeNull();
        theme.Colors!.Primary.ShouldNotBeNullOrWhiteSpace();
        theme.Docx.ShouldNotBeNull();
        theme.Docx!.BaseFontSize.ShouldNotBeNull();
        theme.Docx.Page.ShouldNotBeNull();
    }

    [Fact]
    public void GetPreset_Default_MatchesResolvedThemeDefaults()
    {
        var theme = PresetRegistry.GetPreset("default");

        // Typography should match the hardcoded defaults in ResolvedTheme
        theme.Typography!.HeadingFont.ShouldBe("Calibri");
        theme.Typography.BodyFont.ShouldBe("Cambria");
        theme.Typography.MonoFont.ShouldBe("Cascadia Code");
        theme.Typography.MonoFontFallback.ShouldBe("Consolas");

        // Font sizes
        theme.Docx!.BaseFontSize.ShouldBe(11.0);
        theme.Docx.Heading1Size.ShouldBe(28.0);
        theme.Docx.Heading2Size.ShouldBe(22.0);
        theme.Docx.Heading3Size.ShouldBe(16.0);
        theme.Docx.LineSpacing.ShouldBe(1.15);

        // Page layout
        theme.Docx.Page!.Width.ShouldBe(11906u);
        theme.Docx.Page.Height.ShouldBe(16838u);
    }

    [Fact]
    public void GetPreset_UnknownName_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => PresetRegistry.GetPreset("nonexistent"));
    }

    [Fact]
    public void GetPreset_CaseInsensitive()
    {
        var lower = PresetRegistry.GetPreset("default");
        var upper = PresetRegistry.GetPreset("Default");

        lower.Meta!.Name.ShouldBe(upper.Meta!.Name);
    }

    [Fact]
    public void ListPresets_ReturnsSortedNames()
    {
        var names = PresetRegistry.ListPresets().ToList();

        // At minimum, "default" exists
        names.Count.ShouldBeGreaterThanOrEqualTo(1);

        // Should be sorted
        var sorted = names.OrderBy(n => n).ToList();
        names.ShouldBe(sorted);
    }

    [Fact]
    public void GetPreset_Default_ColorsMatchResolvedThemeDefaults()
    {
        var theme = PresetRegistry.GetPreset("default");

        // Colors should be bare hex (no #) matching ResolvedTheme
        theme.Colors!.Primary.ShouldBe("1B3A5C");
        theme.Colors.Secondary.ShouldBe("4A90D9");
        theme.Colors.BodyText.ShouldBe("333333");
        theme.Colors.CodeBackground.ShouldBe("F5F5F5");
        theme.Colors.CodeBorder.ShouldBe("E0E0E0");
        theme.Colors.Link.ShouldBe("4A90D9");
    }

    [Fact]
    public void GetPreset_ReturnsFreshInstance()
    {
        var first = PresetRegistry.GetPreset("default");
        var second = PresetRegistry.GetPreset("default");

        // Different instances to prevent cache mutation
        ReferenceEquals(first, second).ShouldBeFalse();
    }

    [Fact]
    public void GetPreset_Default_HasAllColorsDefined()
    {
        var theme = PresetRegistry.GetPreset("default");
        var colors = theme.Colors!;

        colors.Primary.ShouldNotBeNullOrWhiteSpace();
        colors.Secondary.ShouldNotBeNullOrWhiteSpace();
        colors.BodyText.ShouldNotBeNullOrWhiteSpace();
        colors.CodeBackground.ShouldNotBeNullOrWhiteSpace();
        colors.CodeBorder.ShouldNotBeNullOrWhiteSpace();
        colors.Link.ShouldNotBeNullOrWhiteSpace();
        colors.TableHeaderBackground.ShouldNotBeNullOrWhiteSpace();
        colors.TableHeaderForeground.ShouldNotBeNullOrWhiteSpace();
        colors.TableBorder.ShouldNotBeNullOrWhiteSpace();
        colors.TableAlternateRow.ShouldNotBeNullOrWhiteSpace();
        colors.BlockquoteBorder.ShouldNotBeNullOrWhiteSpace();
        colors.BlockquoteText.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetPreset_Default_HasAllPageLayoutDefined()
    {
        var theme = PresetRegistry.GetPreset("default");
        var page = theme.Docx!.Page!;

        page.Width.ShouldNotBeNull();
        page.Height.ShouldNotBeNull();
        page.MarginTop.ShouldNotBeNull();
        page.MarginBottom.ShouldNotBeNull();
        page.MarginLeft.ShouldNotBeNull();
        page.MarginRight.ShouldNotBeNull();
    }

    // ----- preset completeness tests (#43) -----

    [Fact]
    public void ListPresets_ReturnsAllPresets()
    {
        var names = PresetRegistry.ListPresets();
        names.Count.ShouldBe(10);
        names.ShouldContain("academic");
        names.ShouldContain("corporate");
        names.ShouldContain("default");
        names.ShouldContain("minimal");
        names.ShouldContain("technical");
        names.ShouldContain("editorial");
        names.ShouldContain("nightowl");
        names.ShouldContain("hackterm");
        names.ShouldContain("bubble");
        names.ShouldContain("rosegarden");
    }

    [Theory]
    [InlineData("technical")]
    [InlineData("corporate")]
    [InlineData("academic")]
    [InlineData("minimal")]
    public void GetPreset_AllPresets_HaveCompleteSections(string presetName)
    {
        var theme = PresetRegistry.GetPreset(presetName);

        theme.Meta.ShouldNotBeNull();
        theme.Meta!.Name.ShouldBe(presetName);
        theme.Typography.ShouldNotBeNull();
        theme.Typography!.HeadingFont.ShouldNotBeNullOrWhiteSpace();
        theme.Typography.BodyFont.ShouldNotBeNullOrWhiteSpace();
        theme.Typography.MonoFont.ShouldNotBeNullOrWhiteSpace();
        theme.Colors.ShouldNotBeNull();
        theme.Colors!.Primary.ShouldNotBeNullOrWhiteSpace();
        theme.Colors.BodyText.ShouldNotBeNullOrWhiteSpace();
        theme.Docx.ShouldNotBeNull();
        theme.Docx!.BaseFontSize.ShouldNotBeNull();
        theme.Docx.Page.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("technical")]
    [InlineData("corporate")]
    [InlineData("academic")]
    [InlineData("minimal")]
    public void GetPreset_AllPresets_HaveAllColorsDefined(string presetName)
    {
        var colors = PresetRegistry.GetPreset(presetName).Colors!;

        colors.Primary.ShouldNotBeNullOrWhiteSpace();
        colors.Secondary.ShouldNotBeNullOrWhiteSpace();
        colors.BodyText.ShouldNotBeNullOrWhiteSpace();
        colors.CodeBackground.ShouldNotBeNullOrWhiteSpace();
        colors.CodeBorder.ShouldNotBeNullOrWhiteSpace();
        colors.Link.ShouldNotBeNullOrWhiteSpace();
        colors.TableHeaderBackground.ShouldNotBeNullOrWhiteSpace();
        colors.TableHeaderForeground.ShouldNotBeNullOrWhiteSpace();
        colors.TableBorder.ShouldNotBeNullOrWhiteSpace();
        colors.TableAlternateRow.ShouldNotBeNullOrWhiteSpace();
        colors.BlockquoteBorder.ShouldNotBeNullOrWhiteSpace();
        colors.BlockquoteText.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("technical")]
    [InlineData("corporate")]
    [InlineData("academic")]
    [InlineData("minimal")]
    public void GetPreset_AllPresets_HaveAllPageLayoutDefined(string presetName)
    {
        var page = PresetRegistry.GetPreset(presetName).Docx!.Page!;

        page.Width.ShouldNotBeNull();
        page.Height.ShouldNotBeNull();
        page.MarginTop.ShouldNotBeNull();
        page.MarginBottom.ShouldNotBeNull();
        page.MarginLeft.ShouldNotBeNull();
        page.MarginRight.ShouldNotBeNull();
    }

    [Fact]
    public void GetPreset_PresetsAreVisuallyDistinct_DifferentPrimaryColors()
    {
        var presets = PresetRegistry.ListPresets();
        var primaries = presets.Select(p => PresetRegistry.GetPreset(p).Colors!.Primary).ToHashSet();

        // All 5 presets should have unique primary colors
        primaries.Count.ShouldBe(presets.Count);
    }

    [Fact]
    public void GetPreset_PresetsAreVisuallyDistinct_DifferentBodyFonts()
    {
        var presets = PresetRegistry.ListPresets();
        var fonts = presets.Select(p => PresetRegistry.GetPreset(p).Typography!.BodyFont).ToList();

        // At least 3 distinct body fonts across 5 presets
        fonts.Distinct().Count().ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void GetPreset_Technical_HasMonospaceHeadings()
    {
        var theme = PresetRegistry.GetPreset("technical");
        theme.Typography!.HeadingFont.ShouldBe("Cascadia Code");
    }

    [Fact]
    public void GetPreset_Academic_HasDoubleSpacing()
    {
        var theme = PresetRegistry.GetPreset("academic");
        theme.Docx!.LineSpacing.ShouldBe(2.0);
    }

    [Fact]
    public void GetPreset_Corporate_HasLargerMargins()
    {
        var theme = PresetRegistry.GetPreset("corporate");
        theme.Docx!.Page!.MarginTop.ShouldBe(1800);
    }

    [Fact]
    public void GetPreset_Minimal_HasThinBorders()
    {
        var theme = PresetRegistry.GetPreset("minimal");
        theme.Docx!.TableBorderWidth.ShouldBe(2);
    }
}
