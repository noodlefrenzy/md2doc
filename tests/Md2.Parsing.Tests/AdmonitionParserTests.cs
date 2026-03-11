// agent-notes: { ctx: "Issue 2 admonition parser tests, TDD red", deps: [Md2.Parsing, Markdig], state: "red", last: "tara@2026-03-11" }

using Markdig;
using Markdig.Syntax;
using Md2.Parsing;
using Shouldly;

namespace Md2.Parsing.Tests;

public class AdmonitionParserTests
{
    private static MarkdownDocument Parse(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .Use<AdmonitionExtension>()
            .Build();
        return Markdown.Parse(markdown, pipeline);
    }

    // ── Basic admonition parsing ───────────────────────────────────────

    [Fact]
    public void Parses_NoteAdmonition()
    {
        var doc = Parse("!!! note\n    This is a note.");

        var admonition = doc.Descendants<AdmonitionBlock>().FirstOrDefault();
        admonition.ShouldNotBeNull();
        admonition!.AdmonitionType.ShouldBe("note");
    }

    [Theory]
    [InlineData("warning")]
    [InlineData("tip")]
    [InlineData("danger")]
    [InlineData("info")]
    public void Parses_VariousAdmonitionTypes(string type)
    {
        var doc = Parse($"!!! {type}\n    Content here.");

        var admonition = doc.Descendants<AdmonitionBlock>().FirstOrDefault();
        admonition.ShouldNotBeNull();
        admonition!.AdmonitionType.ShouldBe(type);
    }

    // ── Custom title ───────────────────────────────────────────────────

    [Fact]
    public void Parses_AdmonitionWithCustomTitle()
    {
        var doc = Parse("!!! warning \"Custom Title\"\n    Content here.");

        var admonition = doc.Descendants<AdmonitionBlock>().FirstOrDefault();
        admonition.ShouldNotBeNull();
        admonition!.AdmonitionType.ShouldBe("warning");
        admonition.Title.ShouldBe("Custom Title");
    }

    [Fact]
    public void Parses_AdmonitionWithoutTitle_TitleIsNull()
    {
        var doc = Parse("!!! note\n    No title here.");

        var admonition = doc.Descendants<AdmonitionBlock>().FirstOrDefault();
        admonition.ShouldNotBeNull();
        admonition!.Title.ShouldBeNull();
    }

    // ── Body content ───────────────────────────────────────────────────

    [Fact]
    public void Parses_AdmonitionWithBodyContent()
    {
        var markdown = "!!! note\n    First paragraph.\n\n    Second paragraph.";
        var doc = Parse(markdown);

        var admonition = doc.Descendants<AdmonitionBlock>().FirstOrDefault();
        admonition.ShouldNotBeNull();
        // AdmonitionBlock is a ContainerBlock, so it should have child blocks
        admonition!.Count.ShouldBeGreaterThan(0);
    }

    // ── Nested admonitions ─────────────────────────────────────────────

    [Fact]
    public void Parses_NestedAdmonitions()
    {
        var markdown = "!!! note\n    Outer content.\n\n    !!! warning\n        Inner content.";
        var doc = Parse(markdown);

        var admonitions = doc.Descendants<AdmonitionBlock>().ToList();
        admonitions.Count.ShouldBeGreaterThanOrEqualTo(2);

        admonitions.ShouldContain(a => a.AdmonitionType == "note");
        admonitions.ShouldContain(a => a.AdmonitionType == "warning");
    }

    // ── Non-admonition content is unaffected ───────────────────────────

    [Fact]
    public void NonAdmonitionContent_IsUnaffected()
    {
        var markdown = "# Heading\n\nRegular paragraph.\n\n!!! note\n    A note.\n\nAnother paragraph.";
        var doc = Parse(markdown);

        doc.OfType<HeadingBlock>().Count().ShouldBe(1);
        doc.OfType<ParagraphBlock>().Count().ShouldBeGreaterThanOrEqualTo(2);
        doc.Descendants<AdmonitionBlock>().Count().ShouldBe(1);
    }

    // ── AdmonitionBlock is ContainerBlock ──────────────────────────────

    [Fact]
    public void AdmonitionBlock_IsContainerBlock()
    {
        var block = new AdmonitionBlock(null!);
        block.ShouldBeAssignableTo<ContainerBlock>();
    }

    [Fact]
    public void AdmonitionBlock_Properties_AreSettable()
    {
        var block = new AdmonitionBlock(null!)
        {
            AdmonitionType = "tip",
            Title = "Pro Tip"
        };

        block.AdmonitionType.ShouldBe("tip");
        block.Title.ShouldBe("Pro Tip");
    }
}
