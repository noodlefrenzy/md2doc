// agent-notes: { ctx: "Issue 7 YamlFrontMatterExtractor transform tests, TDD red", deps: [Md2.Core.Transforms, Md2.Core.Ast, Md2.Parsing], state: "red", last: "tara@2026-03-11" }

using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Parsing;
using Shouldly;

namespace Md2.Core.Tests.Transforms;

public class YamlFrontMatterExtractorTests
{
    private readonly YamlFrontMatterExtractor _sut = new();

    // ── IAstTransform identity ─────────────────────────────────────────

    [Fact]
    public void Name_IsYamlFrontMatterExtractor()
    {
        _sut.Name.ShouldBe("YamlFrontMatterExtractor");
    }

    [Fact]
    public void Order_Is10()
    {
        _sut.Order.ShouldBe(10);
    }

    [Fact]
    public void ImplementsIAstTransform()
    {
        _sut.ShouldBeAssignableTo<IAstTransform>();
    }

    // ── Extraction behavior ────────────────────────────────────────────

    [Fact]
    public void Transform_ExtractsMetadataAndStoresInContext()
    {
        // Arrange
        var markdown = "---\ntitle: My Doc\nauthor: Jane\ndate: 2026-03-11\n---\n\n# Hello";
        var doc = ParseMarkdown(markdown);
        var context = CreateContext();

        // Act
        _sut.Transform(doc, context);

        // Assert
        context.Metadata.ShouldNotBeNull();
        context.Metadata.Title.ShouldBe("My Doc");
        context.Metadata.Author.ShouldBe("Jane");
        context.Metadata.Date.ShouldBe("2026-03-11");
    }

    [Fact]
    public void Transform_StoresMetadataOnAstViaExtension()
    {
        var markdown = "---\ntitle: Test\n---\n\n# Content";
        var doc = ParseMarkdown(markdown);
        var context = CreateContext();

        _sut.Transform(doc, context);

        var metadata = doc.GetDocumentMetadata();
        metadata.ShouldNotBeNull();
        metadata!.Title.ShouldBe("Test");
    }

    [Fact]
    public void Transform_DocumentWithNoFrontMatter_LeavesMetadataDefault()
    {
        var markdown = "# No Front Matter\n\nJust a paragraph.";
        var doc = ParseMarkdown(markdown);
        var context = CreateContext();

        _sut.Transform(doc, context);

        // Metadata should remain at defaults (nulls)
        context.Metadata.Title.ShouldBeNull();
        context.Metadata.Author.ShouldBeNull();
    }

    [Fact]
    public void Transform_RemovesFrontMatterBlockFromAst()
    {
        var markdown = "---\ntitle: Removal Test\n---\n\n# Heading";
        var doc = ParseMarkdown(markdown);
        var context = CreateContext();

        // Verify front matter block exists before transform
        doc.Descendants<YamlFrontMatterBlock>().ShouldNotBeEmpty();

        // Act
        var result = _sut.Transform(doc, context);

        // Assert: front matter block should be removed
        result.Descendants<YamlFrontMatterBlock>().ShouldBeEmpty();
    }

    [Fact]
    public void Transform_PreservesNonFrontMatterContent()
    {
        var markdown = "---\ntitle: Keep Content\n---\n\n# Heading\n\nParagraph text.";
        var doc = ParseMarkdown(markdown);
        var originalNonFmCount = doc.Count(b => b is not YamlFrontMatterBlock);
        var context = CreateContext();

        var result = _sut.Transform(doc, context);

        // Non-front-matter blocks should be preserved
        result.Count.ShouldBe(originalNonFmCount);
    }

    [Fact]
    public void Transform_WithCustomFields_StoresInCustomFieldsDictionary()
    {
        var markdown = "---\ntitle: Custom\nreviewer: Tara\nstatus: draft\n---\n\n# Content";
        var doc = ParseMarkdown(markdown);
        var context = CreateContext();

        _sut.Transform(doc, context);

        context.Metadata.CustomFields.ShouldContainKey("reviewer");
        context.Metadata.CustomFields["reviewer"].ShouldBe("Tara");
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static MarkdownDocument ParseMarkdown(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .Build();
        return Markdown.Parse(markdown, pipeline);
    }

    private static TransformContext CreateContext()
    {
        return new TransformContext(new DocumentMetadata(), new TransformOptions());
    }
}
