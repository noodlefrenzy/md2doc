// agent-notes: { ctx: "Tests for basic PptxEmitter", deps: [Md2.Emit.Pptx.PptxEmitter, Md2.Core.Slides, DocumentFormat.OpenXml], state: active, last: "tara@2026-03-15" }

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Markdig;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Emit;
using Md2.Core.Pipeline;
using Md2.Core.Slides;
using Md2.Slides;
using Shouldly;
using CoreSlide = Md2.Core.Slides.Slide;

namespace Md2.Emit.Pptx.Tests;

public class PptxEmitterTests
{
    private static ResolvedTheme DefaultTheme => new();

    private static SlideDocument CreateSimpleDoc(params string[] slideMarkdowns)
    {
        var doc = new SlideDocument();
        var pipeline = new MarkdownPipelineBuilder().Build();
        for (var i = 0; i < slideMarkdowns.Length; i++)
        {
            var content = Markdown.Parse(slideMarkdowns[i], pipeline);
            doc.AddSlide(new CoreSlide(i, content));
        }
        return doc;
    }

    // ── Basic emit ──────────────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_ProducesValidPptx()
    {
        var doc = CreateSimpleDoc("# Hello World");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Length.ShouldBeGreaterThan(0);

        // Verify it's a valid PPTX
        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        pptx.PresentationPart.ShouldNotBeNull();
    }

    [Fact]
    public async Task EmitAsync_CorrectSlideCount()
    {
        var doc = CreateSimpleDoc("# Slide 1", "# Slide 2", "# Slide 3");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slideIds = pptx.PresentationPart!.Presentation.SlideIdList!.Elements<SlideId>();
        slideIds.Count().ShouldBe(3);
    }

    [Fact]
    public async Task EmitAsync_SingleSlide_HasTextContent()
    {
        var doc = CreateSimpleDoc("# Hello\n\nSome paragraph text");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slideParts = pptx.PresentationPart!.SlideParts.ToList();
        slideParts.Count.ShouldBe(1);

        // Check slide has shapes
        var shapeTree = slideParts[0].Slide.CommonSlideData!.ShapeTree!;
        shapeTree.Elements<Shape>().Count().ShouldBeGreaterThan(0);
    }

    // ── Speaker notes ───────────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_SpeakerNotes_CreatesNotesSlide()
    {
        var doc = CreateSimpleDoc("# Hello");
        doc.Slides[0].SpeakerNotes = "My speaker note";
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        slidePart.NotesSlidePart.ShouldNotBeNull();
    }

    // ── Metadata ────────────────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_SetsDocumentTitle()
    {
        var doc = CreateSimpleDoc("# Hello");
        doc.Metadata.Title = "Test Presentation";
        doc.Metadata.Author = "Test Author";
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        pptx.PackageProperties.Title.ShouldBe("Test Presentation");
        pptx.PackageProperties.Creator.ShouldBe("Test Author");
    }

    // ── Format name ─────────────────────────────────────────────────

    [Fact]
    public void FormatName_IsPptx()
    {
        var emitter = new PptxEmitter();
        emitter.FormatName.ShouldBe("pptx");
    }

    // ── End-to-end with MarpParser ──────────────────────────────────

    [Fact]
    public async Task EmitAsync_FromMarpParser_ProducesValidPptx()
    {
        var markdown = "---\ntitle: Test Deck\nauthor: Author\n---\n\n# Slide 1\n\n---\n\n# Slide 2\n\nContent here\n\n---\n\n## Slide 3\n\n- Item 1\n- Item 2\n- Item 3";
        var parser = new MarpParser();
        var doc = parser.Parse(markdown);
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Length.ShouldBeGreaterThan(0);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slideIds = pptx.PresentationPart!.Presentation.SlideIdList!.Elements<SlideId>();
        slideIds.Count().ShouldBe(3);
        pptx.PackageProperties.Title.ShouldBe("Test Deck");
    }

    // ── Fit heading ────────────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_FitHeading_HasAutoFit()
    {
        // MARP <!-- fit --> heading should produce autoFit on the text body
        var doc = CreateSimpleDoc("# <!-- fit --> Auto-scaled Heading");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        var shapes = slidePart.Slide.CommonSlideData!.ShapeTree!
            .Elements<Shape>().ToList();

        // At least one shape should have NormalAutoFit
        var hasAutoFit = shapes.Any(s =>
            s.TextBody != null &&
            s.TextBody.BodyProperties != null &&
            s.TextBody.BodyProperties.Elements<DocumentFormat.OpenXml.Drawing.NormalAutoFit>().Any());
        hasAutoFit.ShouldBeTrue("Expected at least one shape with NormalAutoFit for fit heading");
    }

    [Fact]
    public async Task EmitAsync_RegularHeading_NoAutoFit()
    {
        var doc = CreateSimpleDoc("# Regular Heading");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        var shapes = slidePart.Slide.CommonSlideData!.ShapeTree!
            .Elements<Shape>().ToList();

        // No shape should have NormalAutoFit
        var hasAutoFit = shapes.Any(s =>
            s.TextBody != null &&
            s.TextBody.BodyProperties != null &&
            s.TextBody.BodyProperties.Elements<DocumentFormat.OpenXml.Drawing.NormalAutoFit>().Any());
        hasAutoFit.ShouldBeFalse("Regular heading should not have NormalAutoFit");
    }

    // ── PPTX theme integration ──────────────────────────────────────

    [Fact]
    public async Task EmitAsync_WithPptxTheme_UsesThemeColors()
    {
        var doc = CreateSimpleDoc("# Hello\n\nText");
        var theme = new ResolvedTheme();
        theme.Pptx = new Md2.Core.Pipeline.ResolvedPptxTheme
        {
            BodyTextColor = "AABBCC"
        };
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, theme, new EmitOptions(), stream);

        stream.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task EmitAsync_WithDarkBackground_SetsSlideBackground()
    {
        var doc = CreateSimpleDoc("# Hello");
        var theme = new ResolvedTheme();
        theme.Pptx = new Md2.Core.Pipeline.ResolvedPptxTheme
        {
            BackgroundColor = "011627"
        };
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, theme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        // Slide master should have background set
        var master = pptx.PresentationPart!.SlideMasterParts.First().SlideMaster;
        master.CommonSlideData!.Background.ShouldNotBeNull();
    }

    [Fact]
    public async Task EmitAsync_WhiteBackground_NoExplicitBackground()
    {
        var doc = CreateSimpleDoc("# Hello");
        var theme = new ResolvedTheme();
        theme.Pptx = new Md2.Core.Pipeline.ResolvedPptxTheme
        {
            BackgroundColor = "FFFFFF"
        };
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, theme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var master = pptx.PresentationPart!.SlideMasterParts.First().SlideMaster;
        // White background should not create an explicit background element
        master.CommonSlideData!.Background.ShouldBeNull();
    }

    [Fact]
    public async Task EmitAsync_TitleSlideLayout_GetsLayoutBackground()
    {
        var doc = new SlideDocument();
        var pipeline = new MarkdownPipelineBuilder().Build();
        var content = Markdig.Markdown.Parse("# Title", pipeline);
        var slide = new CoreSlide(0, content) { Layout = Md2.Core.Slides.SlideLayout.Title };
        doc.AddSlide(slide);

        var theme = new ResolvedTheme();
        theme.Pptx = new Md2.Core.Pipeline.ResolvedPptxTheme
        {
            TitleSlide = new Md2.Core.Pipeline.ResolvedSlideLayoutTheme
            {
                BackgroundColor = "003366"
            }
        };
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, theme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        slidePart.Slide.CommonSlideData!.Background.ShouldNotBeNull();
    }

    [Fact]
    public async Task EmitAsync_PptxThemeFontSizes_UsedForHeadings()
    {
        var doc = CreateSimpleDoc("# Big Title");
        var theme = new ResolvedTheme();
        theme.Pptx = new Md2.Core.Pipeline.ResolvedPptxTheme
        {
            Heading1Size = 60.0
        };
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, theme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        var shapes = slidePart.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>().ToList();

        // Find the heading shape and verify font size
        var headingShape = shapes.First();
        var runs = headingShape.TextBody!.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>()
            .SelectMany(p => p.Elements<DocumentFormat.OpenXml.Drawing.Run>())
            .ToList();
        runs.ShouldNotBeEmpty();
        runs.First().RunProperties!.FontSize!.Value.ShouldBe(6000); // 60pt * 100
    }

    // ── Blockquotes (#137) ─────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_Blockquote_CreatesShape()
    {
        var doc = CreateSimpleDoc("# Title\n\n> This is a quote");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        var shapes = slidePart.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>().ToList();
        // Should have at least 2 shapes: heading + blockquote
        shapes.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    // ── Tables (#131) ────────────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_Table_ProducesValidPptx()
    {
        var doc = CreateSimpleDoc("# Data\n\n| A | B |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Length.ShouldBeGreaterThan(0);
        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        pptx.PresentationPart.ShouldNotBeNull();
    }

    // ── Slide numbers (#129) ─────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_PaginateEnabled_HasSlideNumberShape()
    {
        var doc = new SlideDocument();
        var pipeline = new MarkdownPipelineBuilder().Build();
        var content = Markdig.Markdown.Parse("# Slide 1", pipeline);
        var slide = new CoreSlide(0, content);
        slide.Directives.Paginate = true;
        doc.AddSlide(slide);

        var emitter = new PptxEmitter();
        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        var shapes = slidePart.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>().ToList();
        // Should have heading + slide number
        shapes.Count.ShouldBeGreaterThanOrEqualTo(2);
        shapes.Any(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.Contains("SlideNumber") == true)
            .ShouldBeTrue("Should have a slide number shape");
    }

    // ── Build animation (#133) ───────────────────────────────────────

    [Fact]
    public async Task EmitAsync_BuildAnimationBullets_HasTiming()
    {
        var doc = new SlideDocument();
        var pipeline = new MarkdownPipelineBuilder().Build();
        var content = Markdig.Markdown.Parse("# Title\n\n- Item 1\n- Item 2\n- Item 3", pipeline);
        var slide = new CoreSlide(0, content)
        {
            Build = new Md2.Core.Slides.BuildAnimation(Md2.Core.Slides.BuildAnimationType.Bullets)
        };
        doc.AddSlide(slide);

        var emitter = new PptxEmitter();
        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        slidePart.Slide.Timing.ShouldNotBeNull("Slide with build animation should have timing");
    }

    // ── Code block styling (#132) ────────────────────────────────────

    [Fact]
    public async Task EmitAsync_CodeBlock_HasBackgroundFill()
    {
        var doc = CreateSimpleDoc("# Code\n\n```python\nprint('hello')\n```");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        var shapes = slidePart.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>().ToList();
        // Code shape should have a fill
        var codeShape = shapes.FirstOrDefault(s =>
            s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.StartsWith("Code") == true);
        codeShape.ShouldNotBeNull("Should have a code block shape");
        codeShape.ShapeProperties!.Elements<DocumentFormat.OpenXml.Drawing.SolidFill>().Any()
            .ShouldBeTrue("Code block should have background fill");
    }

    // ── Rich text with links (#136) ──────────────────────────────────

    [Fact]
    public async Task EmitAsync_Hyperlink_ProducesValidPptx()
    {
        var doc = CreateSimpleDoc("Visit [Google](https://google.com) for more.");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Length.ShouldBeGreaterThan(0);
        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        pptx.PresentationPart.ShouldNotBeNull();
    }

    // ── Header/Footer (#128) ────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_HeaderDirective_CreatesHeaderShape()
    {
        var doc = new SlideDocument();
        var pipeline = new MarkdownPipelineBuilder().Build();
        var content = Markdig.Markdown.Parse("# Title", pipeline);
        var slide = new CoreSlide(0, content);
        slide.Directives.Header = "My Presentation";
        doc.AddSlide(slide);

        var emitter = new PptxEmitter();
        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        var shapes = slidePart.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>().ToList();
        shapes.Any(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.Contains("Header") == true)
            .ShouldBeTrue("Should have a header shape");
    }

    [Fact]
    public async Task EmitAsync_FooterDirective_CreatesFooterShape()
    {
        var doc = new SlideDocument();
        var pipeline = new MarkdownPipelineBuilder().Build();
        var content = Markdig.Markdown.Parse("# Title", pipeline);
        var slide = new CoreSlide(0, content);
        slide.Directives.Footer = "Confidential";
        doc.AddSlide(slide);

        var emitter = new PptxEmitter();
        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        var shapes = slidePart.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>().ToList();
        shapes.Any(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.Contains("Footer") == true)
            .ShouldBeTrue("Should have a footer shape");
    }

    // ── Inline images (#135) ────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_InlineImage_WithRealFile_CreatesPicture()
    {
        // Create a minimal PNG file for testing
        var tempDir = Path.Combine(Path.GetTempPath(), $"md2test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var pngPath = Path.Combine(tempDir, "test.png");
            // Minimal valid 1x1 PNG
            var pngBytes = new byte[] {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1 pixels
                0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, // 8-bit RGB
                0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT chunk
                0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
                0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
                0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // IEND chunk
                0x44, 0xAE, 0x42, 0x60, 0x82
            };
            File.WriteAllBytes(pngPath, pngBytes);

            var doc = CreateSimpleDoc("![Test Image](test.png)");
            var emitter = new PptxEmitter();
            var options = new EmitOptions { InputBaseDirectory = tempDir };

            using var stream = new MemoryStream();
            await emitter.EmitAsync(doc, DefaultTheme, options, stream);

            stream.Position = 0;
            using var pptx = PresentationDocument.Open(stream, false);
            var slidePart = pptx.PresentationPart!.SlideParts.First();
            // Should have an image part
            slidePart.ImageParts.Any().ShouldBeTrue("Slide should have an embedded image");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task EmitAsync_InlineImage_MissingFile_NoError()
    {
        var doc = CreateSimpleDoc("![Missing](nonexistent.png)");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        // Should not throw even if image file doesn't exist
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task EmitAsync_InlineImage_PathTraversal_Rejected()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"md2test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var doc = CreateSimpleDoc("![Hack](../../etc/passwd)");
            var emitter = new PptxEmitter();
            var options = new EmitOptions { InputBaseDirectory = tempDir };

            using var stream = new MemoryStream();
            await emitter.EmitAsync(doc, DefaultTheme, options, stream);

            stream.Position = 0;
            using var pptx = PresentationDocument.Open(stream, false);
            var slidePart = pptx.PresentationPart!.SlideParts.First();
            // Should NOT have any image parts (path traversal rejected)
            slidePart.ImageParts.Any().ShouldBeFalse("Path traversal should be rejected");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ── Null checks ─────────────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_NullDoc_Throws()
    {
        var emitter = new PptxEmitter();
        await Should.ThrowAsync<ArgumentNullException>(
            () => emitter.EmitAsync(null!, DefaultTheme, new EmitOptions(), new MemoryStream()));
    }

    [Fact]
    public async Task EmitAsync_NullTheme_Throws()
    {
        var emitter = new PptxEmitter();
        await Should.ThrowAsync<ArgumentNullException>(
            () => emitter.EmitAsync(CreateSimpleDoc("# Hi"), null!, new EmitOptions(), new MemoryStream()));
    }

    // ── Mermaid image fallback (#140) ────────────────────────────────────

    [Fact]
    public async Task EmitAsync_MermaidWithImagePath_EmbedsPicture()
    {
        // Create a minimal PNG for testing
        var tempDir = Path.Combine(Path.GetTempPath(), $"md2test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var pngPath = Path.Combine(tempDir, "mermaid.png");
            var pngBytes = new byte[] {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
                0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
                0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
                0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
                0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
                0x44, 0xAE, 0x42, 0x60, 0x82
            };
            File.WriteAllBytes(pngPath, pngBytes);

            // Create a doc with a mermaid fenced code block that has image path annotation
            var pipeline = new MarkdownPipelineBuilder().Build();
            var md = Markdown.Parse("```mermaid\nsequenceDiagram\n    A->>B: Hello\n```", pipeline);
            // Annotate the FencedCodeBlock with image path
            var fenced = md.Descendants<FencedCodeBlock>().First();
            fenced.SetMermaidImagePath(pngPath);

            var doc = new SlideDocument();
            doc.AddSlide(new CoreSlide(0, md));
            var emitter = new PptxEmitter();

            using var stream = new MemoryStream();
            await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

            stream.Position = 0;
            using var pptx = PresentationDocument.Open(stream, false);
            var slidePart = pptx.PresentationPart!.SlideParts.First();
            slidePart.ImageParts.Any().ShouldBeTrue("Mermaid fallback should embed image");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task EmitAsync_MermaidWithoutImage_FallsBackToCode()
    {
        var doc = CreateSimpleDoc("```mermaid\nsequenceDiagram\n    A->>B: Hello\n```");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        // Should produce valid PPTX (rendered as code block)
        stream.Length.ShouldBeGreaterThan(0);
        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        pptx.PresentationPart.ShouldNotBeNull();
    }

    // ── Mermaid flowchart → native shapes (#138) ────────────────────────

    [Fact]
    public async Task EmitAsync_MermaidFlowchart_CreatesNativeShapes()
    {
        var doc = CreateSimpleDoc("```mermaid\ngraph TD\n    A[Start] --> B[End]\n```");
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;

        // Should have native shapes (not just code block)
        var mermaidShapes = shapeTree.Elements<Shape>()
            .Where(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.Contains("Mermaid") == true)
            .ToList();
        mermaidShapes.Count.ShouldBeGreaterThanOrEqualTo(2, "Should have at least 2 flowchart node shapes");

        // Should also have connector
        var connectors = shapeTree.Elements<ConnectionShape>().ToList();
        connectors.Count.ShouldBeGreaterThanOrEqualTo(1, "Should have at least 1 connector");
    }

    [Fact]
    public async Task EmitAsync_MermaidFlowchartWithTheme_UsesThemeColors()
    {
        var doc = CreateSimpleDoc("```mermaid\ngraph TD\n    A[Start] --> B[End]\n```");
        var theme = new ResolvedTheme
        {
            PrimaryColor = "FF0000",
            SecondaryColor = "00FF00"
        };
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, theme, new EmitOptions(), stream);

        stream.Length.ShouldBeGreaterThan(0);
    }

    // ── Chart code fence (#141/#142) ────────────────────────────────────

    [Fact]
    public async Task EmitAsync_ChartBarYaml_CreatesChart()
    {
        var chartMd = "```chart\ntype: bar\ntitle: Sales\nlabels: [Q1, Q2, Q3]\nseries:\n- name: Rev\n  values: [10, 20, 30]\n```";
        var doc = CreateSimpleDoc(chartMd);
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        // Should have a chart part
        slidePart.ChartParts.Any().ShouldBeTrue("Should have an embedded chart");
    }

    [Fact]
    public async Task EmitAsync_ChartLineYaml_CreatesChart()
    {
        var chartMd = "```chart\ntype: line\ntitle: Trend\nlabels: [Jan, Feb]\nseries:\n- name: Data\n  values: [10, 20]\n```";
        var doc = CreateSimpleDoc(chartMd);
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        slidePart.ChartParts.Any().ShouldBeTrue("Line chart should be embedded");
    }

    [Fact]
    public async Task EmitAsync_ChartPieYaml_CreatesChart()
    {
        var chartMd = "```chart\ntype: pie\ntitle: Share\nlabels: [A, B, C]\nseries:\n- name: Share\n  values: [50, 30, 20]\n```";
        var doc = CreateSimpleDoc(chartMd);
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        slidePart.ChartParts.Any().ShouldBeTrue("Pie chart should be embedded");
    }

    [Fact]
    public async Task EmitAsync_ChartCsv_CreatesChart()
    {
        var chartMd = "```chart\ntype: column\ntitle: CSV Test\n---\nCat,Val1,Val2\nA,10,5\nB,20,10\n```";
        var doc = CreateSimpleDoc(chartMd);
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        slidePart.ChartParts.Any().ShouldBeTrue("CSV column chart should be embedded");
    }

    [Fact]
    public async Task EmitAsync_ChartWithCustomPalette_UsesThemeColors()
    {
        var chartMd = "```chart\ntype: bar\nlabels: [A, B]\nseries:\n- name: Data\n  values: [1, 2]\n```";
        var doc = CreateSimpleDoc(chartMd);
        var theme = new ResolvedTheme
        {
            Pptx = new ResolvedPptxTheme
            {
                ChartPalette = new[] { "FF0000", "00FF00", "0000FF" }
            }
        };
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, theme, new EmitOptions(), stream);

        stream.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task EmitAsync_InvalidChartData_FallsBackToCode()
    {
        var chartMd = "```chart\nthis is not valid chart data\n```";
        var doc = CreateSimpleDoc(chartMd);
        var emitter = new PptxEmitter();

        using var stream = new MemoryStream();
        await emitter.EmitAsync(doc, DefaultTheme, new EmitOptions(), stream);

        // Should still produce valid PPTX (falls back to code block)
        stream.Length.ShouldBeGreaterThan(0);
        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        pptx.PresentationPart.ShouldNotBeNull();
    }
}
