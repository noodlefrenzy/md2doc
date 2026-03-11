// agent-notes: { ctx: "Issue 1 Markdig pipeline config tests, TDD red", deps: [Md2.Parsing, Markdig], state: "red", last: "tara@2026-03-11" }

using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Parsing;
using Shouldly;

namespace Md2.Parsing.Tests;

public class Md2MarkdownPipelineTests
{
    // ── Basic CommonMark ───────────────────────────────────────────────

    [Fact]
    public void Build_ParsesHeadings()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var doc = Markdown.Parse("# H1\n## H2\n### H3", pipeline);

        doc.OfType<HeadingBlock>().Count().ShouldBe(3);
        doc.OfType<HeadingBlock>().First().Level.ShouldBe(1);
    }

    [Fact]
    public void Build_ParsesParagraphs()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var doc = Markdown.Parse("Hello world.\n\nSecond paragraph.", pipeline);

        doc.OfType<ParagraphBlock>().Count().ShouldBe(2);
    }

    [Fact]
    public void Build_ParsesEmphasisAndStrong()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var doc = Markdown.Parse("This is *italic* and **bold**.", pipeline);

        var paragraph = doc.OfType<ParagraphBlock>().First();
        var inlines = paragraph.Inline!.Descendants<EmphasisInline>().ToList();

        inlines.ShouldNotBeEmpty();
    }

    [Fact]
    public void Build_ParsesLinks()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var doc = Markdown.Parse("[Click](https://example.com)", pipeline);

        var paragraph = doc.OfType<ParagraphBlock>().First();
        paragraph.Inline!.Descendants<LinkInline>().ShouldNotBeEmpty();
    }

    // ── GFM Tables ─────────────────────────────────────────────────────

    [Fact]
    public void Build_ParsesGfmTables()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var markdown = "| A | B |\n|---|---|\n| 1 | 2 |";
        var doc = Markdown.Parse(markdown, pipeline);

        doc.OfType<Table>().ShouldNotBeEmpty();
    }

    // ── GFM Strikethrough ──────────────────────────────────────────────

    [Fact]
    public void Build_ParsesGfmStrikethrough()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var doc = Markdown.Parse("This is ~~deleted~~ text.", pipeline);

        var paragraph = doc.OfType<ParagraphBlock>().First();
        // Strikethrough is represented as EmphasisInline with '~' delimiter
        var emphasis = paragraph.Inline!.Descendants<EmphasisInline>()
            .FirstOrDefault(e => e.DelimiterChar == '~');
        emphasis.ShouldNotBeNull();
    }

    // ── GFM Task Lists ────────────────────────────────────────────────

    [Fact]
    public void Build_ParsesGfmTaskLists()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var markdown = "- [x] Done\n- [ ] Not done";
        var doc = Markdown.Parse(markdown, pipeline);

        var taskLists = doc.Descendants<TaskList>().ToList();
        taskLists.ShouldNotBeEmpty();
        taskLists.First().Checked.ShouldBeTrue();
    }

    // ── GFM Autolinks ──────────────────────────────────────────────────

    [Fact]
    public void Build_ParsesGfmAutolinks()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var doc = Markdown.Parse("Visit https://example.com for more.", pipeline);

        var paragraph = doc.OfType<ParagraphBlock>().First();
        paragraph.Inline!.Descendants<LinkInline>().ShouldNotBeEmpty();
    }

    // ── Nested Structures ──────────────────────────────────────────────

    [Fact]
    public void Build_ParsesNestedStructures_ListInsideBlockquote()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var markdown = "> - Item 1\n> - Item 2";
        var doc = Markdown.Parse(markdown, pipeline);

        var quote = doc.OfType<QuoteBlock>().FirstOrDefault();
        quote.ShouldNotBeNull();
        quote!.Descendants<ListItemBlock>().Count().ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Build_ParsesNestedStructures_BlockquoteInsideList()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var markdown = "- Item\n\n  > Quoted text";
        var doc = Markdown.Parse(markdown, pipeline);

        doc.Descendants<QuoteBlock>().ShouldNotBeEmpty();
        doc.Descendants<ListItemBlock>().ShouldNotBeEmpty();
    }

    // ── Source Positions ───────────────────────────────────────────────

    [Fact]
    public void Build_PreservesSourcePositionsOnParsedNodes()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var doc = Markdown.Parse("# Heading\n\nParagraph", pipeline);

        foreach (var block in doc)
        {
            block.Span.Start.ShouldBeGreaterThanOrEqualTo(0);
            block.Span.Length.ShouldBeGreaterThan(0);
        }
    }

    // ── Default options ────────────────────────────────────────────────

    [Fact]
    public void Build_WithNullOptions_UsesDefaults()
    {
        var pipeline = Md2MarkdownPipeline.Build(null);

        pipeline.ShouldNotBeNull();
        pipeline.ShouldBeOfType<MarkdownPipeline>();
    }

    [Fact]
    public void Build_WithCustomOptions_RespectsDisabledGfm()
    {
        var options = new ParserOptions { EnableGfm = false };
        var pipeline = Md2MarkdownPipeline.Build(options);

        // With GFM disabled, strikethrough should not parse
        var doc = Markdown.Parse("~~deleted~~", pipeline);
        var paragraph = doc.OfType<ParagraphBlock>().First();
        var emphasis = paragraph.Inline!.Descendants<EmphasisInline>()
            .FirstOrDefault(e => e.DelimiterChar == '~');
        emphasis.ShouldBeNull();
    }
}
