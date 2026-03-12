// agent-notes: { ctx: "Tests for footnote rendering with bidirectional navigation", deps: [Md2.Emit.Docx, DocumentFormat.OpenXml, Markdig], state: active, last: "tara@2026-03-12" }

using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Md2.Parsing;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class FootnoteTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();

    private MarkdownDocument Parse(string markdown)
    {
        var pipeline = Md2MarkdownPipeline.Build(new ParserOptions());
        return Markdown.Parse(markdown, pipeline);
    }

    [Fact]
    public void Footnote_InlineReference_HasSuperscript()
    {
        var doc = Parse("Text with a footnote[^1].\n\n[^1]: This is the footnote.");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var runs = elements.OfType<Paragraph>().SelectMany(p => p.Descendants<Run>()).ToList();
        runs.Any(r => r.RunProperties?.VerticalTextAlignment != null &&
            r.RunProperties.VerticalTextAlignment.Val?.Value == VerticalPositionValues.Superscript).ShouldBeTrue();
    }

    [Fact]
    public void Footnote_FootnoteSection_HasContent()
    {
        var doc = Parse("Text[^1].\n\n[^1]: Footnote content here.");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var allText = string.Join(" ", elements.SelectMany(e => e.Descendants<Text>()).Select(t => t.Text));
        allText.ShouldContain("Footnote content");
    }

    [Fact]
    public void Footnote_HasSeparator()
    {
        var doc = Parse("Text[^1].\n\n[^1]: A footnote.");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        // Should have a separator (thematic break or border) before footnotes
        var paragraphs = elements.OfType<Paragraph>().ToList();
        paragraphs.Any(p =>
            p.ParagraphProperties?.ParagraphBorders?.TopBorder != null
        ).ShouldBeTrue();
    }

    [Fact]
    public void Footnote_Multiple_AllRendered()
    {
        var doc = Parse("First[^1] and second[^2].\n\n[^1]: Note one.\n[^2]: Note two.");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var allText = string.Join(" ", elements.SelectMany(e => e.Descendants<Text>()).Select(t => t.Text));
        allText.ShouldContain("Note one");
        allText.ShouldContain("Note two");
    }

    [Fact]
    public void Footnote_NumberIsPresent()
    {
        var doc = Parse("Text[^1].\n\n[^1]: A footnote.");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var allText = string.Join("", elements.SelectMany(e => e.Descendants<Text>()).Select(t => t.Text));
        allText.ShouldContain("1");
    }

    [Fact]
    public void Footnote_HasBookmarks()
    {
        var doc = Parse("Text[^1].\n\n[^1]: A footnote.");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        // Should have bookmark starts for navigation
        var bookmarks = elements.SelectMany(e => e.Descendants<BookmarkStart>()).ToList();
        bookmarks.ShouldNotBeEmpty();
    }
}
