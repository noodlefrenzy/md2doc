// agent-notes: { ctx: "Tests for MarpSlideExtractor", deps: [Md2.Slides.MarpSlideExtractor, Markdig, Md2.Core.Slides], state: active, last: "tara@2026-03-15" }

using Markdig;
using Markdig.Syntax;
using Md2.Core.Slides;
using Md2.Slides;
using Md2.Slides.Directives;
using Shouldly;

namespace Md2.Slides.Tests;

public class MarpSlideExtractorTests
{
    private static MarkdownDocument Parse(string markdown)
    {
        // Use pipeline without YAML front matter so --- becomes ThematicBreakBlock
        var pipeline = new MarkdownPipelineBuilder().Build();
        return Markdown.Parse(markdown, pipeline);
    }

    // ── Basic splitting ─────────────────────────────────────────────

    [Fact]
    public void Extract_SingleSlide_NoBreaks_ReturnsOneSlide()
    {
        var doc = Parse("# Hello\n\nWorld");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>());

        result.Slides.Count.ShouldBe(1);
    }

    [Fact]
    public void Extract_TwoSlides_SplitAtHr_ReturnsTwoSlides()
    {
        var doc = Parse("# Slide 1\n\n---\n\n# Slide 2");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>());

        result.Slides.Count.ShouldBe(2);
    }

    [Fact]
    public void Extract_ThreeSlides_ReturnCorrectIndices()
    {
        var doc = Parse("# One\n\n---\n\n# Two\n\n---\n\n# Three");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>());

        result.Slides.Count.ShouldBe(3);
        result.Slides[0].Index.ShouldBe(0);
        result.Slides[1].Index.ShouldBe(1);
        result.Slides[2].Index.ShouldBe(2);
    }

    [Fact]
    public void Extract_SlideContentIsReparented()
    {
        var doc = Parse("# Slide 1\n\nParagraph\n\n---\n\n# Slide 2");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>());

        result.Slides[0].Content.Count.ShouldBe(2); // heading + paragraph
        result.Slides[1].Content.Count.ShouldBe(1); // heading
    }

    // ── Speaker notes ───────────────────────────────────────────────

    [Fact]
    public void Extract_SpeakerNote_AttachedToSlide()
    {
        var doc = Parse("# Hello\n\n<!-- This is a speaker note -->");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>());

        result.Slides[0].SpeakerNotes.ShouldBe("This is a speaker note");
    }

    [Fact]
    public void Extract_MultipleSpeakerNotes_Joined()
    {
        var doc = Parse("# Hello\n\n<!-- Note 1 -->\n\n<!-- Note 2 -->");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>());

        result.Slides[0].SpeakerNotes.ShouldContain("Note 1");
        result.Slides[0].SpeakerNotes.ShouldContain("Note 2");
    }

    [Fact]
    public void Extract_SpeakerNote_NotIncludedInContent()
    {
        var doc = Parse("# Hello\n\n<!-- This is a speaker note -->");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>());

        // Only the heading should be in content, not the speaker note
        result.Slides[0].Content.Count.ShouldBe(1);
    }

    // ── Directive integration ───────────────────────────────────────

    [Fact]
    public void Extract_InlineDirective_AssignedToCorrectSlide()
    {
        var doc = Parse("<!-- class: lead -->\n\n# Hello\n\n---\n\n# World");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>());

        result.Slides[0].Directives.Class.ShouldBe("lead");
        result.Slides[1].Directives.Class.ShouldBe("lead"); // local propagates forward
    }

    [Fact]
    public void Extract_GlobalDirective_AppliesToAllSlides()
    {
        var globals = new List<MarpDirective>
        {
            new("paginate", "true", MarpDirectiveScope.Global)
        };
        var doc = Parse("# One\n\n---\n\n# Two");
        var result = MarpSlideExtractor.Extract(doc, globals);

        result.Slides[0].Directives.Paginate.ShouldBe(true);
        result.Slides[1].Directives.Paginate.ShouldBe(true);
    }

    // ── HeadingDivider ──────────────────────────────────────────────

    [Fact]
    public void Extract_HeadingDivider_SplitsAtHeadings()
    {
        var doc = Parse("# Slide 1\n\nContent\n\n# Slide 2\n\nMore content");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>(), headingDivider: 1);

        result.Slides.Count.ShouldBe(2);
    }

    [Fact]
    public void Extract_HeadingDivider_Level2_SplitsAtH1AndH2()
    {
        var doc = Parse("# H1\n\n## H2\n\nContent");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>(), headingDivider: 2);

        result.Slides.Count.ShouldBe(2);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Extract_NullDoc_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            MarpSlideExtractor.Extract(null!, Array.Empty<MarpDirective>()));
    }

    [Fact]
    public void Extract_EmptyDoc_ReturnsNoSlides()
    {
        var doc = Parse("");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>());

        result.Slides.Count.ShouldBe(0);
    }

    [Fact]
    public void Extract_DirectiveOnlySlide_SkippedFromContent()
    {
        var doc = Parse("<!-- class: lead -->\n\n# Hello");
        var result = MarpSlideExtractor.Extract(doc, Array.Empty<MarpDirective>());

        // The directive HTML block should not be in the slide content
        result.Slides[0].Content.OfType<HtmlBlock>().ShouldBeEmpty();
    }
}
