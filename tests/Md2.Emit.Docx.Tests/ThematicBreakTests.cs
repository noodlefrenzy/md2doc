// agent-notes: { ctx: "Tests for thematic break / horizontal rule rendering", deps: [Md2.Emit.Docx.ParagraphBuilder, DocumentFormat.OpenXml], state: active, last: "tara@2026-03-12" }

using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class ThematicBreakTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();

    [Fact]
    public void CreateThematicBreak_ReturnsParagraph()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateThematicBreak();

        paragraph.ShouldNotBeNull();
        paragraph.ShouldBeOfType<Paragraph>();
    }

    [Fact]
    public void CreateThematicBreak_HasBottomBorder()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateThematicBreak();

        var props = paragraph.ParagraphProperties;
        props.ShouldNotBeNull();
        var borders = props!.ParagraphBorders;
        borders.ShouldNotBeNull();
        var bottom = borders!.BottomBorder;
        bottom.ShouldNotBeNull();
        bottom!.Val!.Value.ShouldBe(BorderValues.Single);
    }

    [Fact]
    public void CreateThematicBreak_HasSpacingAboveAndBelow()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateThematicBreak();

        var props = paragraph.ParagraphProperties;
        var spacing = props!.SpacingBetweenLines;
        spacing.ShouldNotBeNull();
        // Should have some spacing above and below
        int.Parse(spacing!.Before!.Value!).ShouldBeGreaterThan(0);
        int.Parse(spacing!.After!.Value!).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CreateThematicBreak_HasNoTextContent()
    {
        var builder = new ParagraphBuilder(_theme);

        var paragraph = builder.CreateThematicBreak();

        var texts = paragraph.Descendants<Text>().ToList();
        texts.ShouldBeEmpty();
    }
}
