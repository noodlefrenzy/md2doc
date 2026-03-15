// agent-notes: { ctx: "Tests for MarpParser top-level orchestrator", deps: [Md2.Slides.MarpParser, Md2.Core.Slides], state: active, last: "tara@2026-03-15" }

using Md2.Core.Slides;
using Md2.Slides;
using Shouldly;

namespace Md2.Slides.Tests;

public class MarpParserTests
{
    // ── Basic parsing ───────────────────────────────────────────────

    [Fact]
    public void Parse_SimpleMarkdown_ReturnsSlideDocument()
    {
        var parser = new MarpParser();
        var result = parser.Parse("# Hello\n\nWorld");

        result.ShouldNotBeNull();
        result.Slides.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_TwoSlides_SplitByHr()
    {
        var parser = new MarpParser();
        var result = parser.Parse("# Slide 1\n\n---\n\n# Slide 2");

        result.Slides.Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_ThreeSlides_CorrectIndices()
    {
        var parser = new MarpParser();
        var result = parser.Parse("# One\n\n---\n\n# Two\n\n---\n\n# Three");

        result.Slides.Count.ShouldBe(3);
        result.Slides[0].Index.ShouldBe(0);
        result.Slides[1].Index.ShouldBe(1);
        result.Slides[2].Index.ShouldBe(2);
    }

    // ── Front matter ────────────────────────────────────────────────

    [Fact]
    public void Parse_WithFrontMatter_ExtractsMetadata()
    {
        var markdown = "---\ntitle: My Deck\nauthor: Test\ntheme: gaia\n---\n\n# Hello";
        var parser = new MarpParser();
        var result = parser.Parse(markdown);

        result.Metadata.Title.ShouldBe("My Deck");
        result.Metadata.Author.ShouldBe("Test");
        result.Metadata.Theme.ShouldBe("gaia");
    }

    [Fact]
    public void Parse_FrontMatterSize_ParsesCorrectly()
    {
        var markdown = "---\nsize: 4:3\n---\n\n# Hello";
        var parser = new MarpParser();
        var result = parser.Parse(markdown);

        result.Metadata.Size.ShouldBe(SlideSize.Standard4x3);
    }

    [Fact]
    public void Parse_FrontMatterPaginate_CascadesToSlides()
    {
        var markdown = "---\npaginate: true\n---\n\n# Slide 1\n\n---\n\n# Slide 2";
        var parser = new MarpParser();
        var result = parser.Parse(markdown);

        result.Slides[0].Directives.Paginate.ShouldBe(true);
        result.Slides[1].Directives.Paginate.ShouldBe(true);
    }

    [Fact]
    public void Parse_FrontMatterHeadingDivider_SplitsAtHeadings()
    {
        var markdown = "---\nheadingDivider: 1\n---\n\n# Slide 1\n\nContent\n\n# Slide 2\n\nMore";
        var parser = new MarpParser();
        var result = parser.Parse(markdown);

        result.Slides.Count.ShouldBe(2);
    }

    // ── Directives ──────────────────────────────────────────────────

    [Fact]
    public void Parse_InlineDirective_AppliedToSlide()
    {
        var markdown = "<!-- class: lead -->\n\n# Hello";
        var parser = new MarpParser();
        var result = parser.Parse(markdown);

        result.Slides[0].Directives.Class.ShouldBe("lead");
    }

    [Fact]
    public void Parse_ScopedDirective_AppliesOnlyToCurrentSlide()
    {
        var markdown = "# Slide 1\n\n---\n\n<!-- _backgroundColor: aqua -->\n\n# Slide 2\n\n---\n\n# Slide 3";
        var parser = new MarpParser();
        var result = parser.Parse(markdown);

        result.Slides[0].Directives.BackgroundColor.ShouldBeNull();
        result.Slides[1].Directives.BackgroundColor.ShouldBe("aqua");
        result.Slides[2].Directives.BackgroundColor.ShouldBeNull();
    }

    // ── Speaker notes ───────────────────────────────────────────────

    [Fact]
    public void Parse_SpeakerNotes_Extracted()
    {
        var markdown = "# Hello\n\n<!-- This is my note -->";
        var parser = new MarpParser();
        var result = parser.Parse(markdown);

        result.Slides[0].SpeakerNotes.ShouldBe("This is my note");
    }

    // ── Layout inference ────────────────────────────────────────────

    [Fact]
    public void Parse_LeadClass_InfersTitleLayout()
    {
        var markdown = "<!-- class: lead -->\n\n# Big Title";
        var parser = new MarpParser();
        var result = parser.Parse(markdown);

        result.Slides[0].Layout.ShouldBe(SlideLayout.Title);
    }

    [Fact]
    public void Parse_SingleH1_InfersTitleLayout()
    {
        var markdown = "# Title Slide";
        var parser = new MarpParser();
        var result = parser.Parse(markdown);

        result.Slides[0].Layout.ShouldBe(SlideLayout.Title);
    }

    // ── Md2 extensions ──────────────────────────────────────────────

    [Fact]
    public void Parse_Md2BuildExtension_AppliesBuildAnimation()
    {
        var markdown = "<!-- md2: { build: \"bullets\" } -->\n\n# Hello\n\n- One\n- Two";
        var parser = new MarpParser();
        var result = parser.Parse(markdown);

        result.Slides[0].Build.ShouldNotBeNull();
        result.Slides[0].Build!.Type.ShouldBe(BuildAnimationType.Bullets);
    }

    [Fact]
    public void Parse_Md2TransitionExtension_AppliesTransition()
    {
        var markdown = "<!-- md2: { transition: \"fade\" } -->\n\n# Hello";
        var parser = new MarpParser();
        var result = parser.Parse(markdown);

        result.Slides[0].Transition.ShouldNotBeNull();
        result.Slides[0].Transition!.Type.ShouldBe("fade");
    }

    // ── Front matter via Parse ────────────────────────────────────

    [Fact]
    public void Parse_NoFrontMatter_StillProducesSlides()
    {
        var parser = new MarpParser();
        var result = parser.Parse("# Hello\n\nWorld");

        result.Slides.Count.ShouldBe(1);
        result.Metadata.Title.ShouldBeNull();
    }

    // ── Null check ──────────────────────────────────────────────────

    [Fact]
    public void Parse_NullInput_Throws()
    {
        var parser = new MarpParser();
        Should.Throw<ArgumentNullException>(() => parser.Parse(null!));
    }
}
