// agent-notes: { ctx: "TDD tests for ThemeDefinition model", deps: [src/Md2.Themes/ThemeDefinition.cs], state: active, last: "tara@2026-03-12" }

using Shouldly;
using Md2.Themes;

namespace Md2.Themes.Tests;

public class ThemeDefinitionTests
{
    [Fact]
    public void NewDefinition_AllSectionsNull()
    {
        var def = new ThemeDefinition();

        def.Meta.ShouldBeNull();
        def.Typography.ShouldBeNull();
        def.Colors.ShouldBeNull();
        def.Docx.ShouldBeNull();
    }

    [Fact]
    public void MetaSection_AllPropertiesNullable()
    {
        var meta = new ThemeMetaSection();

        meta.Name.ShouldBeNull();
        meta.Description.ShouldBeNull();
        meta.Version.ShouldBeNull();
    }

    [Fact]
    public void TypographySection_AllPropertiesNullable()
    {
        var typo = new ThemeTypographySection();

        typo.HeadingFont.ShouldBeNull();
        typo.BodyFont.ShouldBeNull();
        typo.MonoFont.ShouldBeNull();
        typo.MonoFontFallback.ShouldBeNull();
    }

    [Fact]
    public void ColorsSection_AllPropertiesNullable()
    {
        var colors = new ThemeColorsSection();

        colors.Primary.ShouldBeNull();
        colors.Secondary.ShouldBeNull();
        colors.BodyText.ShouldBeNull();
        colors.CodeBackground.ShouldBeNull();
        colors.CodeBorder.ShouldBeNull();
        colors.Link.ShouldBeNull();
        colors.TableHeaderBackground.ShouldBeNull();
        colors.TableHeaderForeground.ShouldBeNull();
        colors.TableBorder.ShouldBeNull();
        colors.TableAlternateRow.ShouldBeNull();
        colors.BlockquoteBorder.ShouldBeNull();
        colors.BlockquoteText.ShouldBeNull();
    }

    [Fact]
    public void DocxSection_AllPropertiesNullable()
    {
        var docx = new ThemeDocxSection();

        docx.BaseFontSize.ShouldBeNull();
        docx.Heading1Size.ShouldBeNull();
        docx.Heading2Size.ShouldBeNull();
        docx.Heading3Size.ShouldBeNull();
        docx.Heading4Size.ShouldBeNull();
        docx.Heading5Size.ShouldBeNull();
        docx.Heading6Size.ShouldBeNull();
        docx.LineSpacing.ShouldBeNull();
        docx.TableBorderWidth.ShouldBeNull();
        docx.BlockquoteIndentTwips.ShouldBeNull();
        docx.Page.ShouldBeNull();
    }

    [Fact]
    public void PageSection_AllPropertiesNullable()
    {
        var page = new ThemePageSection();

        page.Width.ShouldBeNull();
        page.Height.ShouldBeNull();
        page.MarginTop.ShouldBeNull();
        page.MarginBottom.ShouldBeNull();
        page.MarginLeft.ShouldBeNull();
        page.MarginRight.ShouldBeNull();
    }

    // ── PPTX section ─────────────────────────────────────────────────

    [Fact]
    public void NewDefinition_PptxSectionNull()
    {
        var def = new ThemeDefinition();
        def.Pptx.ShouldBeNull();
    }

    [Fact]
    public void PptxSection_AllPropertiesNullable()
    {
        var pptx = new ThemePptxSection();

        pptx.SlideSize.ShouldBeNull();
        pptx.BaseFontSize.ShouldBeNull();
        pptx.Heading1Size.ShouldBeNull();
        pptx.Heading2Size.ShouldBeNull();
        pptx.Heading3Size.ShouldBeNull();
        pptx.Colors.ShouldBeNull();
        pptx.TitleSlide.ShouldBeNull();
        pptx.SectionDivider.ShouldBeNull();
        pptx.Content.ShouldBeNull();
        pptx.TwoColumn.ShouldBeNull();
        pptx.Background.ShouldBeNull();
        pptx.ChartPalette.ShouldBeNull();
        pptx.CodeBlock.ShouldBeNull();
    }

    [Fact]
    public void PptxTitleSlideSection_BackgroundColorNormalized()
    {
        var ts = new ThemePptxTitleSlideSection { BackgroundColor = "#011627" };
        ts.BackgroundColor.ShouldBe("011627");
    }

    [Fact]
    public void PptxBackgroundSection_ColorNormalized()
    {
        var bg = new ThemePptxBackgroundSection { Color = "#AABBCC" };
        bg.Color.ShouldBe("AABBCC");
    }

    [Fact]
    public void PptxSectionDividerSection_BackgroundColorNormalized()
    {
        var sd = new ThemePptxSectionDividerSection { BackgroundColor = "#0b2942" };
        sd.BackgroundColor.ShouldBe("0b2942");
    }
}
