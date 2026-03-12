// agent-notes: { ctx: "E2E integration test: parse to DOCX roundtrip", deps: [Md2.Core, Md2.Parsing, Md2.Emit.Docx, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Emit.Docx;
using Md2.Parsing;
using Shouldly;

namespace Md2.Integration.Tests;

public class EndToEndTests
{
    private const string RepresentativeMarkdown = @"---
title: Integration Test Document
author: Test Author
date: 2026-03-11
---

# Main Heading

This is a paragraph with **bold**, *italic*, and ~~strikethrough~~ text.

Use `Console.WriteLine` for output.

Visit [Example](https://example.com) for more.

---

## Secondary Heading

Another paragraph with normal body text.

### Tertiary Heading

Final paragraph.
";

    private async Task<(WordprocessingDocument Doc, MemoryStream Stream)> RunFullPipeline(string markdown)
    {
        // Parse
        var pipeline = new ConversionPipeline();
        var parserOptions = new ParserOptions();
        var doc = pipeline.Parse(markdown, parserOptions);

        // Transform (extract front matter)
        pipeline.RegisterTransform(new YamlFrontMatterExtractor());
        var transformOptions = new TransformOptions();
        var transformed = pipeline.Transform(doc, transformOptions);

        // Emit
        var theme = ResolvedTheme.CreateDefault();
        var emitOptions = new EmitOptions();
        var emitter = new DocxEmitter();
        var stream = new MemoryStream();

        await pipeline.Emit(transformed, theme, emitter, emitOptions, stream);
        stream.Position = 0;

        var wordDoc = WordprocessingDocument.Open(stream, false);
        return (wordDoc, stream);
    }

    [Fact]
    public async Task FullPipeline_ProducesValidDocument()
    {
        var (wordDoc, stream) = await RunFullPipeline(RepresentativeMarkdown);
        using var _ = wordDoc;
        using var __ = stream;

        wordDoc.ShouldNotBeNull();
        wordDoc.MainDocumentPart.ShouldNotBeNull();
        wordDoc.MainDocumentPart!.Document.ShouldNotBeNull();
        wordDoc.MainDocumentPart.Document.Body.ShouldNotBeNull();
    }

    [Fact]
    public async Task FullPipeline_ContainsHeadingStyles()
    {
        var (wordDoc, stream) = await RunFullPipeline(RepresentativeMarkdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var paragraphs = body.Elements<Paragraph>().ToList();

        var styleIds = paragraphs
            .Select(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value)
            .Where(s => s != null)
            .ToList();

        styleIds.ShouldContain("Heading1");
        styleIds.ShouldContain("Heading2");
        styleIds.ShouldContain("Heading3");
    }

    [Fact]
    public async Task FullPipeline_ContainsExpectedParagraphText()
    {
        var (wordDoc, stream) = await RunFullPipeline(RepresentativeMarkdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var allText = string.Join(" ", body.Descendants<Text>().Select(t => t.Text));

        allText.ShouldContain("Main Heading");
        allText.ShouldContain("bold");
        allText.ShouldContain("italic");
        allText.ShouldContain("Console.WriteLine");
        allText.ShouldContain("Another paragraph");
    }

    [Fact]
    public async Task FullPipeline_HasCorrectPageLayout()
    {
        var (wordDoc, stream) = await RunFullPipeline(RepresentativeMarkdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var sectionProps = body.Elements<SectionProperties>().FirstOrDefault();
        sectionProps.ShouldNotBeNull();

        var pageSize = sectionProps!.GetFirstChild<PageSize>();
        pageSize.ShouldNotBeNull();
        pageSize!.Width!.Value.ShouldBe(11906U);
        pageSize!.Height!.Value.ShouldBe(16838U);

        var pageMargin = sectionProps.GetFirstChild<PageMargin>();
        pageMargin.ShouldNotBeNull();
        pageMargin!.Top!.Value.ShouldBe(1440);
        pageMargin!.Bottom!.Value.ShouldBe(1440);
    }

    [Fact]
    public async Task FullPipeline_HasPageNumbersInFooter()
    {
        var (wordDoc, stream) = await RunFullPipeline(RepresentativeMarkdown);
        using var _ = wordDoc;
        using var __ = stream;

        var mainPart = wordDoc.MainDocumentPart!;
        var footerParts = mainPart.FooterParts.ToList();
        footerParts.ShouldNotBeEmpty();

        var footer = footerParts.First().Footer;
        var fieldCodes = footer!.Descendants<FieldCode>().ToList();
        fieldCodes.Any(fc => fc.Text.Contains("PAGE")).ShouldBeTrue();
    }

    [Fact]
    public async Task FullPipeline_HasDocumentProperties()
    {
        var (wordDoc, stream) = await RunFullPipeline(RepresentativeMarkdown);
        using var _ = wordDoc;
        using var __ = stream;

        var corePropsPart = wordDoc.CoreFilePropertiesPart;
        corePropsPart.ShouldNotBeNull();
    }

    [Fact]
    public async Task FullPipeline_HasInlineFormatting()
    {
        var (wordDoc, stream) = await RunFullPipeline(RepresentativeMarkdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var runs = body.Descendants<Run>().ToList();

        // Should have bold runs
        runs.Any(r => r.RunProperties?.Bold != null).ShouldBeTrue();

        // Should have italic runs
        runs.Any(r => r.RunProperties?.Italic != null).ShouldBeTrue();

        // Should have strikethrough runs
        runs.Any(r => r.RunProperties?.Strike != null).ShouldBeTrue();

        // Should have code runs with shading
        runs.Any(r => r.RunProperties?.Shading != null).ShouldBeTrue();
    }

    [Fact]
    public async Task FullPipeline_HasHyperlinks()
    {
        var (wordDoc, stream) = await RunFullPipeline(RepresentativeMarkdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var hyperlinks = body.Descendants<Hyperlink>().ToList();
        hyperlinks.ShouldNotBeEmpty();

        var linkText = string.Join("", hyperlinks.First().Descendants<Text>().Select(t => t.Text));
        linkText.ShouldBe("Example");
    }

    [Fact]
    public async Task FullPipeline_HasThematicBreak()
    {
        var (wordDoc, stream) = await RunFullPipeline(RepresentativeMarkdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var paragraphs = body.Elements<Paragraph>().ToList();

        // Should have a paragraph with bottom border (thematic break)
        paragraphs.Any(p =>
            p.ParagraphProperties?.ParagraphBorders?.BottomBorder != null
        ).ShouldBeTrue("Expected a thematic break paragraph with bottom border");
    }

    [Fact]
    public async Task FullPipeline_AllParagraphsHaveWidowControl()
    {
        var (wordDoc, stream) = await RunFullPipeline(RepresentativeMarkdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var paragraphs = body.Elements<Paragraph>().ToList();

        // All content paragraphs (not footer paragraphs) should have widow control
        foreach (var paragraph in paragraphs)
        {
            paragraph.ParagraphProperties.ShouldNotBeNull(
                $"Paragraph missing properties: {string.Join("", paragraph.Descendants<Text>().Select(t => t.Text))}");
            paragraph.ParagraphProperties!.WidowControl.ShouldNotBeNull(
                $"Paragraph missing widow control: {string.Join("", paragraph.Descendants<Text>().Select(t => t.Text))}");
        }
    }
}
