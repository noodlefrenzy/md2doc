// agent-notes: { ctx: "TDD tests for PresetRegistry embedded preset loading", deps: [src/Md2.Themes/PresetRegistry.cs], state: active, last: "tara@2026-03-12" }

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
}
