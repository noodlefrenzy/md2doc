// agent-notes: { ctx: "Tests for blockquote rendering: left border, indentation, nesting", deps: [Md2.Emit.Docx, DocumentFormat.OpenXml, Markdig], state: active, last: "tara@2026-03-12" }

using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Md2.Parsing;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class BlockquoteTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();

    private MarkdownDocument Parse(string markdown)
    {
        var pipeline = Md2MarkdownPipeline.Build(new ParserOptions());
        return Markdown.Parse(markdown, pipeline);
    }

    [Fact]
    public void Blockquote_RendersWithLeftBorder()
    {
        var doc = Parse("> This is a quote");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var paragraph = elements.OfType<Paragraph>().FirstOrDefault();
        paragraph.ShouldNotBeNull();
        var borders = paragraph!.ParagraphProperties?.ParagraphBorders;
        borders.ShouldNotBeNull();
        borders!.LeftBorder.ShouldNotBeNull();
        borders!.LeftBorder!.Val!.Value.ShouldBe(BorderValues.Single);
    }

    [Fact]
    public void Blockquote_HasIndentation()
    {
        var doc = Parse("> This is a quote");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var paragraph = elements.OfType<Paragraph>().FirstOrDefault();
        paragraph.ShouldNotBeNull();
        var indent = paragraph!.ParagraphProperties?.Indentation;
        indent.ShouldNotBeNull();
        int.Parse(indent!.Left!.Value!).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Blockquote_TextHasQuoteColor()
    {
        var doc = Parse("> This is a quote");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var runs = elements.OfType<Paragraph>().SelectMany(p => p.Descendants<Run>()).ToList();
        runs.ShouldNotBeEmpty();
        runs.Any(r => r.RunProperties?.Color?.Val?.Value == _theme.BlockquoteTextColor).ShouldBeTrue();
    }

    [Fact]
    public void Blockquote_ItalicText()
    {
        var doc = Parse("> This is a quote");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var runs = elements.OfType<Paragraph>().SelectMany(p => p.Descendants<Run>()).ToList();
        runs.Any(r => r.RunProperties?.Italic != null).ShouldBeTrue();
    }

    [Fact]
    public void Blockquote_Nested_IncreasesIndent()
    {
        var doc = Parse("> Level 1\n>> Level 2");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var paragraphs = elements.OfType<Paragraph>().ToList();
        paragraphs.Count.ShouldBeGreaterThanOrEqualTo(2);

        var indent1 = int.Parse(paragraphs[0].ParagraphProperties!.Indentation!.Left!.Value!);
        var indent2 = int.Parse(paragraphs[1].ParagraphProperties!.Indentation!.Left!.Value!);
        indent2.ShouldBeGreaterThan(indent1);
    }

    [Fact]
    public void Blockquote_MultiParagraph_AllHaveBorder()
    {
        var doc = Parse("> First paragraph\n>\n> Second paragraph");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var paragraphs = elements.OfType<Paragraph>().ToList();
        paragraphs.Count.ShouldBeGreaterThanOrEqualTo(2);

        foreach (var p in paragraphs)
        {
            p.ParagraphProperties?.ParagraphBorders?.LeftBorder.ShouldNotBeNull();
        }
    }
}
