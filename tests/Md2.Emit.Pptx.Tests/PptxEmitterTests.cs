// agent-notes: { ctx: "Tests for basic PptxEmitter", deps: [Md2.Emit.Pptx.PptxEmitter, Md2.Core.Slides, DocumentFormat.OpenXml], state: active, last: "tara@2026-03-15" }

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Markdig;
using Markdig.Syntax;
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
}
