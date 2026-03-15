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
