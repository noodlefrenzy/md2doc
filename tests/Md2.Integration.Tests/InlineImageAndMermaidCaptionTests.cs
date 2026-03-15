// agent-notes: { ctx: "Integration tests: inline images and Mermaid captions", deps: [Md2.Core, Md2.Parsing, Md2.Emit.Docx, DocumentFormat.OpenXml], state: active, last: "tara@2026-03-13" }

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Emit.Docx;
using Md2.Parsing;
using Shouldly;

namespace Md2.Integration.Tests;

public class InlineImageAndMermaidCaptionTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    /// <summary>
    /// Creates a minimal valid 1x1 white PNG file and returns its absolute path.
    /// </summary>
    private string CreateTempPng()
    {
        // Minimal 1x1 white PNG (67 bytes)
        byte[] pngBytes =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, // 8-bit RGB
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT chunk
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
            0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // IEND chunk
            0x44, 0xAE, 0x42, 0x60, 0x82
        };

        var path = Path.Combine(Path.GetTempPath(), $"md2test_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, pngBytes);
        _tempFiles.Add(path);
        return path;
    }

    private async Task<(WordprocessingDocument Doc, MemoryStream Stream)> RunFullPipeline(string markdown)
    {
        var pipeline = new ConversionPipeline();
        var parserOptions = new ParserOptions();
        var doc = pipeline.Parse(markdown, parserOptions);

        pipeline.RegisterTransform(new YamlFrontMatterExtractor());
        var transformOptions = new TransformOptions();
        var transformResult = pipeline.Transform(doc, transformOptions);

        var theme = ResolvedTheme.CreateDefault();
        var emitOptions = new EmitOptions();
        var emitter = new DocxEmitter();
        var stream = new MemoryStream();

        await pipeline.Emit(transformResult.Document, theme, emitter, emitOptions, stream);
        stream.Position = 0;

        var wordDoc = WordprocessingDocument.Open(stream, false);
        return (wordDoc, stream);
    }

    private async Task<(WordprocessingDocument Doc, MemoryStream Stream)> RunFullPipelineWithAstMutation(
        string markdown, Action<MarkdownDocument> mutateAst, string? inputBaseDirectory = null)
    {
        var pipeline = new ConversionPipeline();
        var parserOptions = new ParserOptions();
        var doc = pipeline.Parse(markdown, parserOptions);

        pipeline.RegisterTransform(new YamlFrontMatterExtractor());
        var transformOptions = new TransformOptions();
        var transformResult = pipeline.Transform(doc, transformOptions);

        // Apply AST mutation (e.g., annotate mermaid blocks with image paths)
        mutateAst(transformResult.Document);

        var theme = ResolvedTheme.CreateDefault();
        var emitOptions = new EmitOptions { InputBaseDirectory = inputBaseDirectory };
        var emitter = new DocxEmitter();
        var stream = new MemoryStream();

        await pipeline.Emit(transformResult.Document, theme, emitter, emitOptions, stream);
        stream.Position = 0;

        var wordDoc = WordprocessingDocument.Open(stream, false);
        return (wordDoc, stream);
    }

    // -----------------------------------------------------------------------
    // Edge Case 1: Inline images must not produce nested Paragraphs
    // -----------------------------------------------------------------------

    [Fact]
    public async Task InlineImage_DoesNotProduceNestedParagraphs()
    {
        var imagePath = CreateTempPng();
        var markdown = $"Text before ![alt text]({imagePath}) text after.\n";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var __ = stream;
        using var _ = wordDoc;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var paragraphs = body.Elements<Paragraph>().ToList();

        // No Paragraph should contain another Paragraph as a descendant
        foreach (var p in paragraphs)
        {
            var nestedParagraphs = p.Descendants<Paragraph>().ToList();
            nestedParagraphs.ShouldBeEmpty(
                "Inline image must not produce nested Paragraph elements (invalid OOXML)");
        }
    }

    [Fact]
    public async Task InlineImage_DrawingElementExistsAsRunChild()
    {
        var imagePath = CreateTempPng();
        var markdown = $"Text before ![alt text]({imagePath}) text after.\n";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var __ = stream;
        using var _ = wordDoc;

        var body = wordDoc.MainDocumentPart!.Document.Body!;

        // The Drawing element should exist within a Run (not directly in a Paragraph)
        var drawings = body.Descendants<Drawing>().ToList();
        drawings.ShouldNotBeEmpty("Expected at least one Drawing element for the inline image");

        foreach (var drawing in drawings)
        {
            drawing.Parent.ShouldBeOfType<Run>(
                "Drawing element must be a child of a Run, not directly in a Paragraph");
        }
    }

    [Fact]
    public async Task InlineImage_PreservesTextBeforeAndAfterImage()
    {
        var imagePath = CreateTempPng();
        var markdown = $"Text before ![alt text]({imagePath}) text after.\n";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var __ = stream;
        using var _ = wordDoc;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var allText = string.Join("", body.Descendants<Text>().Select(t => t.Text));

        allText.ShouldContain("Text before");
        allText.ShouldContain("text after.");
    }

    [Fact]
    public async Task InlineImage_SurroundingTextAndDrawingShareSameParagraph()
    {
        var imagePath = CreateTempPng();
        var markdown = $"Text before ![alt text]({imagePath}) text after.\n";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var __ = stream;
        using var _ = wordDoc;

        var body = wordDoc.MainDocumentPart!.Document.Body!;

        // Find the paragraph that contains "Text before"
        var paragraphWithText = body.Elements<Paragraph>()
            .FirstOrDefault(p => p.Descendants<Text>().Any(t => t.Text.Contains("Text before")));

        paragraphWithText.ShouldNotBeNull("Expected a paragraph containing 'Text before'");

        // The same paragraph should contain the Drawing element
        var drawingsInParagraph = paragraphWithText!.Descendants<Drawing>().ToList();
        drawingsInParagraph.ShouldNotBeEmpty(
            "The Drawing for the inline image should be in the same paragraph as the surrounding text");

        // The same paragraph should also contain "text after"
        var textElements = paragraphWithText.Descendants<Text>().Select(t => t.Text).ToList();
        string.Join("", textElements).ShouldContain("text after.");
    }

    // -----------------------------------------------------------------------
    // Edge Case 2: Mermaid diagram blocks must not produce visible captions
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MermaidBlock_DoesNotProduceVisibleCaption()
    {
        var imagePath = CreateTempPng();
        var markdown = "# Test\n\n```mermaid\ngraph TD;\n    A-->B;\n```\n";

        var (wordDoc, stream) = await RunFullPipelineWithAstMutation(markdown, doc =>
        {
            // Find the fenced code block and annotate it with a mermaid image path,
            // simulating what MermaidDiagramRenderer does at runtime.
            var fencedBlock = doc.Descendants<FencedCodeBlock>()
                .FirstOrDefault(b => b.Info == "mermaid");
            fencedBlock.ShouldNotBeNull("Expected a mermaid fenced code block in the AST");
            fencedBlock!.SetMermaidImagePath(imagePath);
        });

        using var __ = stream;
        using var _ = wordDoc;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var allText = string.Join(" ", body.Descendants<Text>().Select(t => t.Text));

        // The text "Mermaid diagram" should NOT appear as visible caption text
        allText.ShouldNotContain("Mermaid diagram");
    }

    [Fact]
    public async Task MermaidBlock_ProducesImageDrawingElement()
    {
        var imagePath = CreateTempPng();
        var markdown = "# Test\n\n```mermaid\ngraph TD;\n    A-->B;\n```\n";

        var (wordDoc, stream) = await RunFullPipelineWithAstMutation(markdown, doc =>
        {
            var fencedBlock = doc.Descendants<FencedCodeBlock>()
                .FirstOrDefault(b => b.Info == "mermaid");
            fencedBlock.ShouldNotBeNull("Expected a mermaid fenced code block in the AST");
            fencedBlock!.SetMermaidImagePath(imagePath);
        });

        using var __ = stream;
        using var _ = wordDoc;

        var body = wordDoc.MainDocumentPart!.Document.Body!;

        // Even without a caption, the image Drawing should still be present
        var drawings = body.Descendants<Drawing>().ToList();
        drawings.ShouldNotBeEmpty("Expected a Drawing element for the Mermaid diagram image");
    }

    [Fact]
    public async Task MermaidBlock_ImageParagraphHasNoCaptionSibling()
    {
        var imagePath = CreateTempPng();
        var markdown = "```mermaid\ngraph TD;\n    A-->B;\n```\n";

        var (wordDoc, stream) = await RunFullPipelineWithAstMutation(markdown, doc =>
        {
            var fencedBlock = doc.Descendants<FencedCodeBlock>()
                .FirstOrDefault(b => b.Info == "mermaid");
            fencedBlock.ShouldNotBeNull("Expected a mermaid fenced code block in the AST");
            fencedBlock!.SetMermaidImagePath(imagePath);
        });

        using var __ = stream;
        using var _ = wordDoc;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var paragraphs = body.Elements<Paragraph>().ToList();

        // Find the paragraph that contains a Drawing (the mermaid image)
        var imageParagraph = paragraphs
            .FirstOrDefault(p => p.Descendants<Drawing>().Any());
        imageParagraph.ShouldNotBeNull("Expected an image paragraph with Drawing element");

        // The next sibling paragraph (if any) should NOT be a caption-style paragraph
        // containing alt text. Since alt is empty, there should be no caption paragraph
        // immediately after the image paragraph.
        var nextElement = imageParagraph!.NextSibling<Paragraph>();
        if (nextElement != null)
        {
            var nextText = string.Join("", nextElement.Descendants<Text>().Select(t => t.Text));
            // A caption would contain the alt text — which for mermaid is empty, so
            // we verify no "mermaid" text appears in the next paragraph
            nextText.ToLowerInvariant().ShouldNotContain("mermaid");
        }
    }

    // -----------------------------------------------------------------------
    // Regression: Mermaid images must embed even when InputBaseDirectory is set
    // The path safety check must not reject absolute cache paths for images
    // we rendered ourselves.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MermaidBlock_EmbedsImage_WhenInputBaseDirectoryIsSet()
    {
        var imagePath = CreateTempPng(); // absolute path in /tmp
        var markdown = "```mermaid\ngraph TD;\n    A-->B;\n```\n";

        // Use a different directory as base — simulates real CLI usage where
        // the input .md file lives in a project directory, not /tmp
        var baseDir = Path.Combine(Path.GetTempPath(), $"md2test_base_{Guid.NewGuid():N}");
        Directory.CreateDirectory(baseDir);

        try
        {
            var (wordDoc, stream) = await RunFullPipelineWithAstMutation(
                markdown,
                doc =>
                {
                    var fencedBlock = doc.Descendants<FencedCodeBlock>()
                        .FirstOrDefault(b => b.Info == "mermaid");
                    fencedBlock.ShouldNotBeNull();
                    fencedBlock!.SetMermaidImagePath(imagePath);
                },
                inputBaseDirectory: baseDir);

            using var __ = stream;
            using var _ = wordDoc;

            var body = wordDoc.MainDocumentPart!.Document.Body!;

            // The image should be embedded as a Drawing, not replaced with a placeholder
            var drawings = body.Descendants<Drawing>().ToList();
            drawings.ShouldNotBeEmpty(
                "Mermaid diagram should embed as a Drawing even when InputBaseDirectory is set");

            // Verify no placeholder text was emitted
            var allText = string.Join(" ", body.Descendants<Text>().Select(t => t.Text));
            allText.ShouldNotContain("Image not found");
        }
        finally
        {
            try { Directory.Delete(baseDir, true); }
            catch { /* best-effort cleanup */ }
        }
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try { File.Delete(path); }
            catch { /* best-effort cleanup */ }
        }
    }
}
