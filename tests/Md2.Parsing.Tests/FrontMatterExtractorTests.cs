// agent-notes: { ctx: "Issue 4 YAML front matter extraction tests, TDD red", deps: [Md2.Parsing, Md2.Core.Ast, Markdig, YamlDotNet], state: "red", last: "tara@2026-03-11" }

using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Parsing;
using Shouldly;

namespace Md2.Parsing.Tests;

public class FrontMatterExtractorTests
{
    private static MarkdownDocument ParseWithFrontMatter(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .Build();
        return Markdown.Parse(markdown, pipeline);
    }

    // ── Standard extraction ────────────────────────────────────────────

    [Fact]
    public void Extract_StandardFields_ReturnsPopulatedMetadata()
    {
        var markdown = "---\ntitle: My Document\nauthor: Jane Doe\ndate: 2026-03-11\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        var metadata = FrontMatterExtractor.Extract(doc);

        metadata.ShouldNotBeNull();
        metadata.Title.ShouldBe("My Document");
        metadata.Author.ShouldBe("Jane Doe");
        metadata.Date.ShouldBe("2026-03-11");
    }

    [Fact]
    public void Extract_SubjectAndKeywords_AreExtracted()
    {
        var markdown = "---\nsubject: Testing\nkeywords: md2, docx, converter\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        var metadata = FrontMatterExtractor.Extract(doc);

        metadata.Subject.ShouldBe("Testing");
        metadata.Keywords.ShouldBe("md2, docx, converter");
    }

    // ── Missing fields ─────────────────────────────────────────────────

    [Fact]
    public void Extract_MissingFields_ReturnsNullForMissing()
    {
        var markdown = "---\ntitle: Only Title\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        var metadata = FrontMatterExtractor.Extract(doc);

        metadata.Title.ShouldBe("Only Title");
        metadata.Author.ShouldBeNull();
        metadata.Date.ShouldBeNull();
        metadata.Subject.ShouldBeNull();
        metadata.Keywords.ShouldBeNull();
    }

    // ── Custom fields ──────────────────────────────────────────────────

    [Fact]
    public void Extract_UnknownFields_PreservedInCustomFields()
    {
        var markdown = "---\ntitle: Doc\nreviewer: Tara\nstatus: draft\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        var metadata = FrontMatterExtractor.Extract(doc);

        metadata.CustomFields.ShouldContainKey("reviewer");
        metadata.CustomFields["reviewer"].ShouldBe("Tara");
        metadata.CustomFields.ShouldContainKey("status");
        metadata.CustomFields["status"].ShouldBe("draft");
    }

    [Fact]
    public void Extract_OnlyStandardFields_CustomFieldsIsEmpty()
    {
        var markdown = "---\ntitle: Doc\nauthor: Jane\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        var metadata = FrontMatterExtractor.Extract(doc);

        metadata.CustomFields.ShouldBeEmpty();
    }

    // ── Malformed YAML ─────────────────────────────────────────────────

    [Fact]
    public void Extract_MalformedYaml_ThrowsWithLineInfo()
    {
        var markdown = "---\ntitle: Bad YAML\n  invalid:\n    - misaligned: [\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        var ex = Should.Throw<FrontMatterParseException>(() => FrontMatterExtractor.Extract(doc));
        ex.Message.ShouldNotBeNullOrEmpty();
        ex.LineNumber.ShouldBeGreaterThan(0);
    }

    // ── No front matter ────────────────────────────────────────────────

    [Fact]
    public void Extract_NoFrontMatter_ReturnsEmptyMetadata()
    {
        var markdown = "# Just a heading\n\nNo front matter here.";
        var doc = ParseWithFrontMatter(markdown);

        var metadata = FrontMatterExtractor.Extract(doc);

        metadata.ShouldNotBeNull();
        metadata.Title.ShouldBeNull();
        metadata.Author.ShouldBeNull();
        metadata.Date.ShouldBeNull();
        metadata.CustomFields.ShouldBeEmpty();
    }

    // ── Multiline values ───────────────────────────────────────────────

    [Fact]
    public void Extract_MultilineValues_HandledCorrectly()
    {
        var markdown = "---\ntitle: |\n  This is a\n  multiline title\nauthor: Jane\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        var metadata = FrontMatterExtractor.Extract(doc);

        metadata.Title.ShouldNotBeNull();
        metadata.Title!.ShouldContain("This is a");
        metadata.Title.ShouldContain("multiline title");
    }

    [Fact]
    public void Extract_FoldedMultilineValues_HandledCorrectly()
    {
        var markdown = "---\ntitle: >\n  This is a\n  folded title\nauthor: Jane\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        var metadata = FrontMatterExtractor.Extract(doc);

        metadata.Title.ShouldNotBeNull();
        metadata.Title!.ShouldContain("This is a");
    }

    // ── Edge cases ─────────────────────────────────────────────────────

    [Fact]
    public void Extract_EmptyFrontMatter_ReturnsEmptyMetadata()
    {
        var markdown = "---\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        var metadata = FrontMatterExtractor.Extract(doc);

        metadata.ShouldNotBeNull();
        metadata.Title.ShouldBeNull();
        metadata.CustomFields.ShouldBeEmpty();
    }
}
