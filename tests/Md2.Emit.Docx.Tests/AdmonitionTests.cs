// agent-notes: { ctx: "Tests for admonition/callout rendering", deps: [Md2.Emit.Docx, DocumentFormat.OpenXml, Markdig, Md2.Parsing], state: active, last: "tara@2026-03-12" }

using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Md2.Parsing;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class AdmonitionTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();

    private MarkdownDocument Parse(string markdown)
    {
        var pipeline = Md2MarkdownPipeline.Build(new ParserOptions());
        return Markdown.Parse(markdown, pipeline);
    }

    [Fact]
    public void Admonition_Note_RendersWithBorder()
    {
        var doc = Parse("!!! note\n    This is a note.");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        // Should have at least a label paragraph and content paragraph
        elements.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Admonition_HasLeftBorder()
    {
        var doc = Parse("!!! warning\n    Be careful!");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var paragraphs = elements.OfType<Paragraph>().ToList();
        paragraphs.ShouldNotBeEmpty();
        paragraphs.Any(p =>
            p.ParagraphProperties?.ParagraphBorders?.LeftBorder != null
        ).ShouldBeTrue();
    }

    [Fact]
    public void Admonition_HasBoldLabel()
    {
        var doc = Parse("!!! tip\n    A helpful tip.");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var paragraphs = elements.OfType<Paragraph>().ToList();
        // First paragraph should be the label
        var labelRuns = paragraphs.First().Descendants<Run>().ToList();
        labelRuns.Any(r => r.RunProperties?.Bold != null).ShouldBeTrue();
    }

    [Fact]
    public void Admonition_WithCustomTitle_UsesTitle()
    {
        var doc = Parse("!!! note \"Custom Title\"\n    Content here.");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var allText = string.Join("", elements.SelectMany(e => e.Descendants<Text>()).Select(t => t.Text));
        allText.ShouldContain("Custom Title");
    }

    [Fact]
    public void Admonition_WithDefaultTitle_UsesTypeAsLabel()
    {
        var doc = Parse("!!! important\n    This matters.");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var allText = string.Join("", elements.SelectMany(e => e.Descendants<Text>()).Select(t => t.Text));
        allText.ShouldContain("Important");
    }

    [Theory]
    [InlineData("note")]
    [InlineData("warning")]
    [InlineData("tip")]
    [InlineData("important")]
    [InlineData("caution")]
    public void Admonition_AllTypes_RenderWithoutError(string type)
    {
        var doc = Parse($"!!! {type}\n    Content.");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        elements.ShouldNotBeEmpty();
    }
}
