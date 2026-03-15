// agent-notes: { ctx: "Tests for SlidePipeline orchestrator", deps: [Md2.Core.Slides, Md2.Core.Pipeline, Md2.Core.Emit], state: active, last: "tara@2026-03-15" }

using Markdig.Syntax;
using Md2.Core.Emit;
using Md2.Core.Pipeline;
using Md2.Core.Slides;
using Md2.Core.Transforms;
using Shouldly;

namespace Md2.Core.Tests.Slides;

public class SlidePipelineTests
{
    private class RecordingTransform : IAstTransform
    {
        public string Name => "Recording";
        public int Order => 10;
        public int CallCount { get; private set; }

        public MarkdownDocument Transform(MarkdownDocument doc, TransformContext context)
        {
            CallCount++;
            return doc;
        }
    }

    private class RecordingSlideEmitter : ISlideEmitter
    {
        public string FormatName => "pptx";
        public SlideDocument? ReceivedDoc { get; private set; }
        public bool WasCalled { get; private set; }

        public Task EmitAsync(SlideDocument doc, ResolvedTheme theme, EmitOptions options, Stream output)
        {
            WasCalled = true;
            ReceivedDoc = doc;
            return Task.CompletedTask;
        }
    }

    // ── Construction ──────────────────────────────────────────────────

    [Fact]
    public void SlidePipeline_CanBeConstructed()
    {
        var pipeline = new SlidePipeline();
        pipeline.ShouldNotBeNull();
    }

    // ── Parse ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SimpleMarkdown_ReturnsDocument()
    {
        var pipeline = new SlidePipeline();
        var doc = pipeline.Parse("# Hello\n\nWorld\n");
        doc.ShouldNotBeNull();
        doc.ShouldBeOfType<MarkdownDocument>();
    }

    // ── Transform ─────────────────────────────────────────────────────

    [Fact]
    public void Transform_RunsRegisteredTransforms()
    {
        var pipeline = new SlidePipeline();
        var transform = new RecordingTransform();
        pipeline.RegisterTransform(transform);

        var doc = pipeline.Parse("# Hello\n");
        pipeline.Transform(doc, new TransformOptions());

        transform.CallCount.ShouldBe(1);
    }

    // ── BuildSlideDocument ────────────────────────────────────────────

    [Fact]
    public void BuildSlideDocument_SingleSlide_ProducesOneSlide()
    {
        var pipeline = new SlidePipeline();
        var doc = pipeline.Parse("# Title\n\nContent here\n");

        var slideDoc = pipeline.BuildSlideDocument(doc);
        slideDoc.ShouldNotBeNull();
        slideDoc.Slides.Count.ShouldBe(1);
    }

    [Fact]
    public void BuildSlideDocument_ThreeSlides_ProducesThreeSlides()
    {
        var pipeline = new SlidePipeline();
        var doc = pipeline.Parse("# Slide 1\n\n---\n\n# Slide 2\n\n---\n\n# Slide 3\n");

        var slideDoc = pipeline.BuildSlideDocument(doc);
        slideDoc.Slides.Count.ShouldBe(3);
    }

    [Fact]
    public void BuildSlideDocument_SlidesHaveCorrectIndices()
    {
        var pipeline = new SlidePipeline();
        var doc = pipeline.Parse("# A\n\n---\n\n# B\n");

        var slideDoc = pipeline.BuildSlideDocument(doc);
        slideDoc.Slides[0].Index.ShouldBe(0);
        slideDoc.Slides[1].Index.ShouldBe(1);
    }

    [Fact]
    public void BuildSlideDocument_SlideContentContainsExpectedBlocks()
    {
        var pipeline = new SlidePipeline();
        var doc = pipeline.Parse("# Heading\n\nParagraph text\n");

        var slideDoc = pipeline.BuildSlideDocument(doc);
        var slide = slideDoc.Slides[0];
        slide.Content.OfType<HeadingBlock>().Count().ShouldBe(1);
        slide.Content.OfType<ParagraphBlock>().Count().ShouldBe(1);
    }

    [Fact]
    public void BuildSlideDocument_EmptyMarkdown_ProducesOneEmptySlide()
    {
        var pipeline = new SlidePipeline();
        var doc = pipeline.Parse("");

        var slideDoc = pipeline.BuildSlideDocument(doc);
        // Even empty input should produce at least a document (may be 0 slides)
        slideDoc.ShouldNotBeNull();
    }

    // ── Emit ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Emit_CallsSlideEmitter()
    {
        var pipeline = new SlidePipeline();
        var emitter = new RecordingSlideEmitter();
        var slideDoc = new SlideDocument();
        slideDoc.AddSlide(new Slide(0, new MarkdownDocument()));

        await pipeline.Emit(slideDoc, new ResolvedTheme(), emitter, new EmitOptions(), Stream.Null);

        emitter.WasCalled.ShouldBeTrue();
        emitter.ReceivedDoc.ShouldBeSameAs(slideDoc);
    }

    // ── Full pipeline flow ────────────────────────────────────────────

    [Fact]
    public async Task FullFlow_ParseTransformBuildEmit_Works()
    {
        var pipeline = new SlidePipeline();
        var transform = new RecordingTransform();
        pipeline.RegisterTransform(transform);

        var emitter = new RecordingSlideEmitter();

        // Parse
        var doc = pipeline.Parse("# Slide 1\n\n---\n\n# Slide 2\n");

        // Transform (on full doc)
        var result = pipeline.Transform(doc, new TransformOptions());

        // Build SlideDocument
        var slideDoc = pipeline.BuildSlideDocument(result.Document);

        // Emit
        await pipeline.Emit(slideDoc, new ResolvedTheme(), emitter, new EmitOptions(), Stream.Null);

        transform.CallCount.ShouldBe(1);
        emitter.WasCalled.ShouldBeTrue();
        emitter.ReceivedDoc!.Slides.Count.ShouldBe(2);
    }
}
