// agent-notes: { ctx: "Detailed tests for ParagraphBuilder heading/body", deps: [Md2.Emit.Docx.ParagraphBuilder, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class ParagraphBuilderTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();

    // ── Heading paragraphs ──────────────────────────────────────────

    [Theory]
    [InlineData(1, "Heading1")]
    [InlineData(2, "Heading2")]
    [InlineData(3, "Heading3")]
    [InlineData(4, "Heading4")]
    [InlineData(5, "Heading5")]
    [InlineData(6, "Heading6")]
    public void CreateHeadingParagraph_SetsCorrectStyleId(int level, string expectedStyleId)
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateHeadingParagraph(level, "Test");

        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        styleId.ShouldBe(expectedStyleId);
    }

    [Fact]
    public void CreateHeadingParagraph_HasWidowControl()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateHeadingParagraph(1, "Test");

        paragraph.ParagraphProperties!.WidowControl.ShouldNotBeNull();
    }

    [Fact]
    public void CreateHeadingParagraph_HasCorrectText()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateHeadingParagraph(1, "My Heading");

        var text = string.Join("", paragraph.Descendants<Text>().Select(t => t.Text));
        text.ShouldBe("My Heading");
    }

    [Fact]
    public void CreateHeadingParagraph_HasCorrectFontSize()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateHeadingParagraph(1, "Test");

        var run = paragraph.GetFirstChild<Run>();
        run.ShouldNotBeNull();
        var fontSize = run!.RunProperties?.FontSize?.Val?.Value;
        // 28pt = 56 half-points
        fontSize.ShouldBe("56");
    }

    [Fact]
    public void CreateHeadingParagraph_UsesHeadingFont()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateHeadingParagraph(1, "Test");

        var run = paragraph.GetFirstChild<Run>();
        var runFonts = run!.RunProperties?.RunFonts;
        runFonts.ShouldNotBeNull();
        runFonts!.Ascii!.Value.ShouldBe("Calibri");
    }

    [Fact]
    public void CreateHeadingParagraph_HasPrimaryColor()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateHeadingParagraph(1, "Test");

        var run = paragraph.GetFirstChild<Run>();
        var color = run!.RunProperties?.Color?.Val?.Value;
        color.ShouldBe("1B3A5C");
    }

    [Fact]
    public void CreateHeadingParagraph_HasOutlineLevel()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateHeadingParagraph(2, "Test");

        var outlineLevel = paragraph.ParagraphProperties?.OutlineLevel;
        outlineLevel.ShouldNotBeNull();
        // Outline level is 0-based: Heading1 = 0, Heading2 = 1, etc.
        outlineLevel!.Val!.Value.ShouldBe(1);
    }

    // ── Body paragraphs ─────────────────────────────────────────────

    [Fact]
    public void CreateBodyParagraph_UsesNormalStyle()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateBodyParagraph();

        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        styleId.ShouldBe("Normal");
    }

    [Fact]
    public void CreateBodyParagraph_HasWidowControl()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateBodyParagraph();

        paragraph.ParagraphProperties!.WidowControl.ShouldNotBeNull();
    }

    // ── Run creation ────────────────────────────────────────────────

    [Fact]
    public void CreateRun_PlainText_HasBodyFont()
    {
        var builder = new ParagraphBuilder(_theme);

        var run = builder.CreateRun("Hello");

        var runFonts = run.RunProperties?.RunFonts;
        runFonts.ShouldNotBeNull();
        runFonts!.Ascii!.Value.ShouldBe("Cambria");
    }

    [Fact]
    public void CreateRun_WithBold_HasBoldProperty()
    {
        var builder = new ParagraphBuilder(_theme);

        var run = builder.CreateRun("text", bold: true);

        run.RunProperties?.Bold.ShouldNotBeNull();
    }

    [Fact]
    public void CreateRun_WithItalic_HasItalicProperty()
    {
        var builder = new ParagraphBuilder(_theme);

        var run = builder.CreateRun("text", italic: true);

        run.RunProperties?.Italic.ShouldNotBeNull();
    }

    [Fact]
    public void CreateRun_WithStrikethrough_HasStrikeProperty()
    {
        var builder = new ParagraphBuilder(_theme);

        var run = builder.CreateRun("text", strikethrough: true);

        run.RunProperties?.Strike.ShouldNotBeNull();
    }

    [Fact]
    public void CreateRun_TextHasPreserveSpace()
    {
        var builder = new ParagraphBuilder(_theme);

        var run = builder.CreateRun("Hello world");

        var text = run.GetFirstChild<Text>();
        text.ShouldNotBeNull();
        text!.Space!.Value.ShouldBe(SpaceProcessingModeValues.Preserve);
    }

    // ── Inline code run ─────────────────────────────────────────────

    [Fact]
    public void CreateInlineCodeRun_HasMonoFont()
    {
        var builder = new ParagraphBuilder(_theme);

        var run = builder.CreateInlineCodeRun("var x");

        var runFonts = run.RunProperties?.RunFonts;
        runFonts.ShouldNotBeNull();
        runFonts!.Ascii!.Value.ShouldBe("Cascadia Code");
    }

    [Fact]
    public void CreateInlineCodeRun_HasBackgroundShading()
    {
        var builder = new ParagraphBuilder(_theme);

        var run = builder.CreateInlineCodeRun("var x");

        var shading = run.RunProperties?.Shading;
        shading.ShouldNotBeNull();
        shading!.Fill!.Value.ShouldBe("F5F5F5");
        shading.Val!.Value.ShouldBe(ShadingPatternValues.Clear);
    }
}
