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
}
