// agent-notes: { ctx: "Tests for SmartTypographyTransform: curly quotes, dashes, ellipsis", deps: [Md2.Core.Transforms.SmartTypographyTransform, Markdig], state: active, last: "sato@2026-03-11" }

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Core.Ast;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Shouldly;

namespace Md2.Core.Tests.Transforms;

public class SmartTypographyTransformTests
{
    private readonly SmartTypographyTransform _sut = new();

    [Fact]
    public void Name_IsSmartTypography()
    {
        _sut.Name.ShouldBe("SmartTypography");
    }

    [Fact]
    public void Order_Is20()
    {
        _sut.Order.ShouldBe(20);
    }

    [Fact]
    public void ImplementsIAstTransform()
    {
        _sut.ShouldBeAssignableTo<IAstTransform>();
    }

    [Fact]
    public void Transform_DoubleQuotes_BecomeCurly()
    {
        var doc = Parse("He said \"hello\" to her.");
        var context = CreateContext();

        _sut.Transform(doc, context);

        var text = ExtractText(doc);
        text.ShouldContain("\u201C"); // "
        text.ShouldContain("\u201D"); // "
        text.ShouldNotContain("\"");
    }

    [Fact]
    public void Transform_SingleQuotes_BecomeCurly()
    {
        var doc = Parse("It's a nice day.");
        var context = CreateContext();

        _sut.Transform(doc, context);

        var text = ExtractText(doc);
        text.ShouldContain("\u2019"); // '  (right single quote for apostrophe)
    }

    [Fact]
    public void Transform_DoubleDash_BecomesEnDash()
    {
        var doc = Parse("Pages 10--20 are relevant.");
        var context = CreateContext();

        _sut.Transform(doc, context);

        var text = ExtractText(doc);
        text.ShouldContain("\u2013"); // –
        text.ShouldNotContain("--");
    }

    [Fact]
    public void Transform_TripleDash_BecomesEmDash()
    {
        var doc = Parse("She paused---then continued.");
        var context = CreateContext();

        _sut.Transform(doc, context);

        var text = ExtractText(doc);
        text.ShouldContain("\u2014"); // —
        text.ShouldNotContain("---");
    }

    [Fact]
    public void Transform_Ellipsis_BecomesUnicodeEllipsis()
    {
        var doc = Parse("Wait for it...");
        var context = CreateContext();

        _sut.Transform(doc, context);

        var text = ExtractText(doc);
        text.ShouldContain("\u2026"); // …
    }

    [Fact]
    public void Transform_CodeSpan_NotTransformed()
    {
        var doc = Parse("Normal \"text\" and `code \"quotes\" here`.");
        var context = CreateContext();

        _sut.Transform(doc, context);

        // The code inline should still have straight quotes
        var codeInlines = doc.Descendants<CodeInline>().ToList();
        codeInlines.ShouldNotBeEmpty();
        codeInlines[0].Content.ShouldContain("\"");
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static MarkdownDocument Parse(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        return Markdown.Parse(markdown, pipeline);
    }

    private static TransformContext CreateContext()
    {
        return new TransformContext(new DocumentMetadata(), new TransformOptions());
    }

    private static string ExtractText(MarkdownDocument doc)
    {
        var literals = doc.Descendants<LiteralInline>().ToList();
        return string.Join("", literals.Select(l => l.Content.ToString()));
    }
}
