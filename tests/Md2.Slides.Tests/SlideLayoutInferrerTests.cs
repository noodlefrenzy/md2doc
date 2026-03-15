// agent-notes: { ctx: "Tests for SlideLayoutInferrer", deps: [Md2.Slides.SlideLayoutInferrer, Md2.Core.Slides], state: active, last: "tara@2026-03-15" }

using Markdig;
using Markdig.Syntax;
using Md2.Core.Slides;
using Md2.Slides;
using Shouldly;

namespace Md2.Slides.Tests;

public class SlideLayoutInferrerTests
{
    private static Slide CreateSlide(string markdown, string? classDirective = null)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var doc = Markdown.Parse(markdown, pipeline);
        var slide = new Slide(0, doc);
        if (classDirective != null)
            slide.Directives = new SlideDirectives { Class = classDirective };
        return slide;
    }

    // ── Md2 extension layout override ───────────────────────────────

    [Fact]
    public void Infer_Md2ExtensionLayout_TakesPriority()
    {
        var slide = CreateSlide("# Hello", classDirective: "lead");
        var ext = new Md2Extension { Layout = "blank" };

        var layout = SlideLayoutInferrer.Infer(slide, ext);
        layout.ShouldBe(SlideLayout.Blank);
    }

    // ── Class directive mapping ─────────────────────────────────────

    [Fact]
    public void Infer_LeadClass_ReturnsTitle()
    {
        var slide = CreateSlide("# Hello", classDirective: "lead");
        var layout = SlideLayoutInferrer.Infer(slide);
        layout.ShouldBe(SlideLayout.Title);
    }

    [Fact]
    public void Infer_TitleClass_ReturnsTitle()
    {
        var slide = CreateSlide("# Hello", classDirective: "title");
        var layout = SlideLayoutInferrer.Infer(slide);
        layout.ShouldBe(SlideLayout.Title);
    }

    [Fact]
    public void Infer_InvertClass_ReturnsContent()
    {
        var slide = CreateSlide("# Hello\n\nBody", classDirective: "invert");
        var layout = SlideLayoutInferrer.Infer(slide);
        layout.ShouldBe(SlideLayout.Content);
    }

    [Fact]
    public void Infer_SplitClass_ReturnsTwoColumn()
    {
        var slide = CreateSlide("# Hello", classDirective: "split");
        var layout = SlideLayoutInferrer.Infer(slide);
        layout.ShouldBe(SlideLayout.TwoColumn);
    }

    [Fact]
    public void Infer_UnknownClass_ReturnsCustomLayout()
    {
        var slide = CreateSlide("# Hello", classDirective: "custom-style");
        var layout = SlideLayoutInferrer.Infer(slide);
        layout.Name.ShouldBe("custom-style");
    }

    // ── Content heuristics ──────────────────────────────────────────

    [Fact]
    public void Infer_SingleH1_ReturnsTitle()
    {
        var slide = CreateSlide("# Big Title");
        var layout = SlideLayoutInferrer.Infer(slide);
        layout.ShouldBe(SlideLayout.Title);
    }

    [Fact]
    public void Infer_H1WithShortParagraph_ReturnsTitle()
    {
        var slide = CreateSlide("# Title\n\nSubtitle text");
        var layout = SlideLayoutInferrer.Infer(slide);
        layout.ShouldBe(SlideLayout.Title);
    }

    [Fact]
    public void Infer_H1WithLongContent_ReturnsContent()
    {
        var longText = new string('x', 150);
        var slide = CreateSlide($"# Title\n\n{longText}");
        var layout = SlideLayoutInferrer.Infer(slide);
        layout.ShouldBe(SlideLayout.Content);
    }

    [Fact]
    public void Infer_BulletList_ReturnsContent()
    {
        var slide = CreateSlide("## Topics\n\n- One\n- Two\n- Three");
        var layout = SlideLayoutInferrer.Infer(slide);
        layout.ShouldBe(SlideLayout.Content);
    }

    [Fact]
    public void Infer_EmptyContent_ReturnsBlank()
    {
        var slide = new Slide(0, new MarkdownDocument());
        var layout = SlideLayoutInferrer.Infer(slide);
        layout.ShouldBe(SlideLayout.Blank);
    }

    // ── ResolveLayoutName ───────────────────────────────────────────

    [Theory]
    [InlineData("content", "content")]
    [InlineData("title", "title")]
    [InlineData("two-column", "two-column")]
    [InlineData("section-divider", "section-divider")]
    [InlineData("section", "section-divider")]
    [InlineData("blank", "blank")]
    public void ResolveLayoutName_KnownNames_ReturnsCorrect(string input, string expectedName)
    {
        var layout = SlideLayoutInferrer.ResolveLayoutName(input);
        layout.Name.ShouldBe(expectedName);
    }

    [Fact]
    public void ResolveLayoutName_Unknown_ReturnsCustom()
    {
        var layout = SlideLayoutInferrer.ResolveLayoutName("my-custom");
        layout.Name.ShouldBe("my-custom");
    }

    [Fact]
    public void ResolveLayoutName_Null_ReturnsContent()
    {
        var layout = SlideLayoutInferrer.ResolveLayoutName(null!);
        layout.ShouldBe(SlideLayout.Content);
    }

    // ── Null checks ─────────────────────────────────────────────────

    [Fact]
    public void Infer_NullSlide_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            SlideLayoutInferrer.Infer(null!));
    }
}
