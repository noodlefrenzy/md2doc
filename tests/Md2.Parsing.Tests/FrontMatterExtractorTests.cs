// agent-notes: { ctx: "Issue 4 YAML front matter extraction tests, TDD red", deps: [Md2.Parsing, Md2.Core.Ast, Markdig, YamlDotNet], state: "red", last: "tara@2026-03-14" }

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

    // ── H-1: YAML type tag safety ───────────────────────────────────────

    [Fact]
    public void Extract_YamlBinaryTag_RejectedSafely()
    {
        // !!binary is a YAML type tag that could trigger unsafe deserialization
        // with object targets. With Dictionary<string, string> deserialization,
        // unknown tags are safely rejected by throwing FrontMatterParseException.
        var markdown = "---\ntitle: Safe Doc\npayload: !!binary aGVsbG8=\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        Should.Throw<FrontMatterParseException>(() => FrontMatterExtractor.Extract(doc));
    }

    [Fact]
    public void Extract_YamlCustomTag_RejectedSafely()
    {
        // Custom YAML tags like !!python/object must never trigger type instantiation.
        // With strongly-typed deserialization (Dictionary<string, string>), these tags
        // are safely rejected with a FrontMatterParseException.
        var markdown = "---\ntitle: Tag Test\nevil: !!python/object:os.system 'echo pwned'\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        Should.Throw<FrontMatterParseException>(() => FrontMatterExtractor.Extract(doc));
    }

    [Fact]
    public void Extract_PlainStringsOnly_NoObjectDeserialization()
    {
        // Verify that all front matter values are strings — no object, binary, or
        // complex types can sneak through via the deserializer.
        var markdown = "---\ntitle: Plain Test\nauthor: Jane\ncustom: some value\nnumber: 42\n---\n\n# Content";
        var doc = ParseWithFrontMatter(markdown);

        var metadata = FrontMatterExtractor.Extract(doc);

        metadata.ShouldNotBeNull();
        metadata.Title.ShouldBe("Plain Test");
        metadata.Author.ShouldBe("Jane");
        metadata.CustomFields["custom"].ShouldBe("some value");
        metadata.CustomFields["number"].ShouldBe("42");
    }
}
