// agent-notes: { ctx: "Issue 6 typed AST extensions tests, TDD red", deps: [Md2.Core.Ast, Markdig.Syntax], state: "red", last: "tara@2026-03-11" }

using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Core.Ast;
using Shouldly;

namespace Md2.Core.Tests.Ast;

public class AstExtensionsTests
{
    // ── AstDataKeys ────────────────────────────────────────────────────

    [Fact]
    public void AstDataKeys_AllKeysAreDefined()
    {
        AstDataKeys.SyntaxTokens.ShouldNotBeNullOrEmpty();
        AstDataKeys.MermaidImagePath.ShouldNotBeNullOrEmpty();
        AstDataKeys.OmmlXml.ShouldNotBeNullOrEmpty();
        AstDataKeys.DocumentMetadata.ShouldNotBeNullOrEmpty();
        AstDataKeys.MathFallback.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void AstDataKeys_AllKeysAreUnique()
    {
        var keys = new[]
        {
            AstDataKeys.SyntaxTokens,
            AstDataKeys.MermaidImagePath,
            AstDataKeys.OmmlXml,
            AstDataKeys.DocumentMetadata,
            AstDataKeys.MathFallback
        };

        keys.Distinct().Count().ShouldBe(keys.Length);
    }

    // ── SyntaxTokens round-trip ────────────────────────────────────────

    [Fact]
    public void SetSyntaxTokens_GetSyntaxTokens_RoundTrips()
    {
        var block = new FencedCodeBlock(null!);
        var tokens = new List<SyntaxToken>
        {
            new("public", "#0000FF", SyntaxFontStyle.Bold),
            new(" class ", null, SyntaxFontStyle.Normal),
            new("Foo", "#008000", SyntaxFontStyle.Normal),
        };

        block.SetSyntaxTokens(tokens);
        var result = block.GetSyntaxTokens();

        result.ShouldNotBeNull();
        result!.Count.ShouldBe(3);
        result[0].Text.ShouldBe("public");
        result[0].ForegroundColor.ShouldBe("#0000FF");
        result[0].FontStyle.ShouldBe(SyntaxFontStyle.Bold);
        result[1].ForegroundColor.ShouldBeNull();
    }

    [Fact]
    public void GetSyntaxTokens_WhenNotSet_ReturnsNull()
    {
        var block = new FencedCodeBlock(null!);

        block.GetSyntaxTokens().ShouldBeNull();
    }

    // ── MermaidImagePath round-trip ────────────────────────────────────

    [Fact]
    public void SetMermaidImagePath_GetMermaidImagePath_RoundTrips()
    {
        var block = new FencedCodeBlock(null!);
        var path = "/tmp/diagrams/mermaid-abc123.png";

        block.SetMermaidImagePath(path);

        block.GetMermaidImagePath().ShouldBe(path);
    }

    [Fact]
    public void GetMermaidImagePath_WhenNotSet_ReturnsNull()
    {
        var block = new FencedCodeBlock(null!);

        block.GetMermaidImagePath().ShouldBeNull();
    }

    // ── OmmlXml round-trip ─────────────────────────────────────────────

    [Fact]
    public void SetOmmlXml_GetOmmlXml_RoundTrips()
    {
        var block = new FencedCodeBlock(null!);
        var omml = "<m:oMath><m:r><m:t>x</m:t></m:r></m:oMath>";

        block.SetOmmlXml(omml);

        block.GetOmmlXml().ShouldBe(omml);
    }

    [Fact]
    public void GetOmmlXml_WhenNotSet_ReturnsNull()
    {
        var block = new FencedCodeBlock(null!);

        block.GetOmmlXml().ShouldBeNull();
    }

    // ── DocumentMetadata round-trip ────────────────────────────────────

    [Fact]
    public void SetDocumentMetadata_GetDocumentMetadata_RoundTrips()
    {
        var doc = new MarkdownDocument();
        var metadata = new DocumentMetadata
        {
            Title = "My Title",
            Author = "Jane Doe",
            Date = "2026-03-11",
            Subject = "Testing",
            Keywords = "md2, docx, test",
            CustomFields = new Dictionary<string, string> { ["reviewer"] = "Tara" }.AsReadOnly()
        };

        doc.SetDocumentMetadata(metadata);
        var result = doc.GetDocumentMetadata();

        result.ShouldNotBeNull();
        result!.Title.ShouldBe("My Title");
        result.Author.ShouldBe("Jane Doe");
        result.Date.ShouldBe("2026-03-11");
        result.Subject.ShouldBe("Testing");
        result.Keywords.ShouldBe("md2, docx, test");
        result.CustomFields["reviewer"].ShouldBe("Tara");
    }

    [Fact]
    public void GetDocumentMetadata_WhenNotSet_ReturnsNull()
    {
        var doc = new MarkdownDocument();

        doc.GetDocumentMetadata().ShouldBeNull();
    }

    // ── DocumentMetadata defaults ──────────────────────────────────────

    [Fact]
    public void DocumentMetadata_DefaultValues_AllNullOrEmpty()
    {
        var metadata = new DocumentMetadata();

        metadata.Title.ShouldBeNull();
        metadata.Author.ShouldBeNull();
        metadata.Date.ShouldBeNull();
        metadata.Subject.ShouldBeNull();
        metadata.Keywords.ShouldBeNull();
        metadata.CustomFields.ShouldNotBeNull();
        metadata.CustomFields.Count.ShouldBe(0);
    }

    // ── SyntaxToken record ─────────────────────────────────────────────

    [Fact]
    public void SyntaxToken_RecordEquality()
    {
        var a = new SyntaxToken("text", "#FF0000", SyntaxFontStyle.Bold);
        var b = new SyntaxToken("text", "#FF0000", SyntaxFontStyle.Bold);

        a.ShouldBe(b);
    }

    // ── SyntaxFontStyle enum ───────────────────────────────────────────

    [Theory]
    [InlineData(SyntaxFontStyle.Normal, 0)]
    [InlineData(SyntaxFontStyle.Bold, 1)]
    [InlineData(SyntaxFontStyle.Italic, 2)]
    [InlineData(SyntaxFontStyle.BoldItalic, 3)]
    public void SyntaxFontStyle_HasExpectedValues(SyntaxFontStyle style, int expected)
    {
        ((int)style).ShouldBe(expected);
    }
}
