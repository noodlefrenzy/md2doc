// agent-notes: { ctx: "Tests for MarpDirectiveExtractor", deps: [Md2.Slides.Directives.MarpDirectiveExtractor, Markdig], state: active, last: "tara@2026-03-15" }

using Markdig;
using Markdig.Syntax;
using Md2.Slides.Directives;
using Shouldly;

namespace Md2.Slides.Tests.Directives;

public class MarpDirectiveExtractorTests
{
    private static MarkdownDocument Parse(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        return Markdown.Parse(markdown, pipeline);
    }

    // ── Extract from HtmlBlock ──────────────────────────────────────

    [Fact]
    public void Extract_SingleDirective_ReturnsOne()
    {
        var doc = Parse("<!-- class: invert -->\n\n# Hello");
        var directives = MarpDirectiveExtractor.Extract(doc);

        directives.Count.ShouldBe(1);
        directives[0].Key.ShouldBe("class");
        directives[0].Value.ShouldBe("invert");
    }

    [Fact]
    public void Extract_ScopedDirective_PreservesUnderscorePrefix()
    {
        var doc = Parse("<!-- _backgroundColor: aqua -->\n\n# Hello");
        var directives = MarpDirectiveExtractor.Extract(doc);

        directives.Count.ShouldBe(1);
        directives[0].Key.ShouldBe("_backgroundColor");
        directives[0].Value.ShouldBe("aqua");
    }

    [Fact]
    public void Extract_MultipleDirectives_ReturnsAll()
    {
        var doc = Parse("<!-- class: lead -->\n\n<!-- paginate: true -->\n\n# Hello");
        var directives = MarpDirectiveExtractor.Extract(doc);

        directives.Count.ShouldBe(2);
        directives[0].Key.ShouldBe("class");
        directives[1].Key.ShouldBe("paginate");
    }

    [Fact]
    public void Extract_MultiLineComment_CapturesAllDirectives()
    {
        var doc = Parse("<!--\nclass: lead\nbackgroundColor: #abc\npaginate: true\n-->\n\n# Hello");
        var directives = MarpDirectiveExtractor.Extract(doc);

        directives.Count.ShouldBe(3);
        directives[0].Key.ShouldBe("class");
        directives[0].Value.ShouldBe("lead");
        directives[1].Key.ShouldBe("backgroundColor");
        directives[1].Value.ShouldBe("#abc");
        directives[2].Key.ShouldBe("paginate");
        directives[2].Value.ShouldBe("true");
    }

    [Fact]
    public void Extract_NoDirectives_ReturnsEmpty()
    {
        var doc = Parse("# Hello\n\nWorld");
        var directives = MarpDirectiveExtractor.Extract(doc);

        directives.ShouldBeEmpty();
    }

    [Fact]
    public void Extract_SpeakerNoteComment_IsSkipped()
    {
        var doc = Parse("<!-- This is a speaker note -->\n\n# Hello");
        var directives = MarpDirectiveExtractor.Extract(doc);

        directives.ShouldBeEmpty();
    }

    [Fact]
    public void Extract_DirectiveWithSpaces_TrimsValue()
    {
        var doc = Parse("<!-- color:   red   -->\n\n# Hello");
        var directives = MarpDirectiveExtractor.Extract(doc);

        directives.Count.ShouldBe(1);
        directives[0].Value.ShouldBe("red");
    }

    [Fact]
    public void Extract_PaginateDirective_ReturnsBoolString()
    {
        var doc = Parse("<!-- paginate: true -->\n\n# Hello");
        var directives = MarpDirectiveExtractor.Extract(doc);

        directives.Count.ShouldBe(1);
        directives[0].Key.ShouldBe("paginate");
        directives[0].Value.ShouldBe("true");
    }

    [Fact]
    public void Extract_NullDoc_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            MarpDirectiveExtractor.Extract(null!));
    }

    // ── Extract from front matter ───────────────────────────────────

    [Fact]
    public void ExtractFromFrontMatter_ReturnsGlobalScope()
    {
        var frontMatter = new Dictionary<string, string>
        {
            ["theme"] = "gaia",
            ["paginate"] = "true"
        };

        var directives = MarpDirectiveExtractor.ExtractFromFrontMatter(frontMatter);

        directives.Count.ShouldBe(2);
        directives.ShouldAllBe(d => d.Scope == MarpDirectiveScope.Global);
        directives.ShouldAllBe(d => d.SlideIndex == -1);
    }

    [Fact]
    public void ExtractFromFrontMatter_NullInput_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            MarpDirectiveExtractor.ExtractFromFrontMatter(null!));
    }

    // ── Speaker note detection ──────────────────────────────────────

    [Fact]
    public void IsSpeakerNote_PlainComment_ReturnsTrue()
    {
        MarpDirectiveExtractor.IsSpeakerNote("<!-- This is my note -->").ShouldBeTrue();
    }

    [Fact]
    public void IsSpeakerNote_DirectiveComment_ReturnsFalse()
    {
        MarpDirectiveExtractor.IsSpeakerNote("<!-- class: invert -->").ShouldBeFalse();
    }

    [Fact]
    public void IsSpeakerNote_EmptyComment_ReturnsFalse()
    {
        MarpDirectiveExtractor.IsSpeakerNote("<!-- -->").ShouldBeFalse();
    }

    [Fact]
    public void IsSpeakerNote_NotComment_ReturnsFalse()
    {
        MarpDirectiveExtractor.IsSpeakerNote("<div>hello</div>").ShouldBeFalse();
    }

    [Fact]
    public void ExtractSpeakerNote_PlainComment_ReturnsContent()
    {
        var note = MarpDirectiveExtractor.ExtractSpeakerNote("<!-- My speaker note -->");
        note.ShouldBe("My speaker note");
    }

    [Fact]
    public void ExtractSpeakerNote_DirectiveComment_ReturnsNull()
    {
        var note = MarpDirectiveExtractor.ExtractSpeakerNote("<!-- class: invert -->");
        note.ShouldBeNull();
    }
}
