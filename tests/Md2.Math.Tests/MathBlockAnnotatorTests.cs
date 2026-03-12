// agent-notes: { ctx: "Red-phase tests for MathBlockAnnotator AST transform", deps: [Md2.Math.MathBlockAnnotator, Md2.Math.LatexToOmmlConverter, Md2.Diagrams.BrowserManager, Md2.Core.Transforms, Md2.Core.Ast, Markdig, Shouldly], state: red, last: "tara@2026-03-12" }

using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Core.Ast;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Diagrams;
using Shouldly;

namespace Md2.Math.Tests;

[Trait("Category", "Integration")]
public class MathBlockAnnotatorTests : IAsyncDisposable
{
    private readonly BrowserManager _browserManager;
    private readonly LatexToOmmlConverter _converter;

    public MathBlockAnnotatorTests()
    {
        _browserManager = new BrowserManager();
        _converter = new LatexToOmmlConverter(_browserManager);
    }

    public async ValueTask DisposeAsync()
    {
        await _browserManager.DisposeAsync();
    }

    private static MarkdownDocument ParseWithMath(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseMathematics()
            .Build();
        return Markdown.Parse(markdown, pipeline);
    }

    private static TransformContext CreateContext()
    {
        var metadata = new DocumentMetadata { Title = "Test" };
        var options = new TransformOptions { RenderMermaid = false };
        return new TransformContext(metadata, options);
    }

    private MathBlockAnnotator CreateTransform()
    {
        return new MathBlockAnnotator(_converter);
    }

    [Fact]
    public void Transform_AnnotatesDisplayMath_WithOmml()
    {
        // Multi-line $$ creates MathBlock in Markdig
        var markdown = "Some text\n\n$$\nx^2 + y^2 = z^2\n$$\n\nMore text";
        var doc = ParseWithMath(markdown);
        var context = CreateContext();
        var transform = CreateTransform();

        transform.Transform(doc, context);

        var mathBlock = doc.Descendants<MathBlock>().SingleOrDefault();
        mathBlock.ShouldNotBeNull();
        var omml = mathBlock.GetOmmlXml();
        omml.ShouldNotBeNullOrEmpty();
        omml.ShouldContain("<m:oMath");
    }

    [Fact]
    public void Transform_AnnotatesInlineMath_WithOmml()
    {
        var markdown = "The equation $x^2$ is simple.";
        var doc = ParseWithMath(markdown);
        var context = CreateContext();
        var transform = CreateTransform();

        transform.Transform(doc, context);

        var mathInline = doc.Descendants<MathInline>().SingleOrDefault();
        mathInline.ShouldNotBeNull();
        var omml = mathInline.GetOmmlXml();
        omml.ShouldNotBeNullOrEmpty();
        omml.ShouldContain("<m:oMath");
    }

    [Fact]
    public void Transform_SkipsNonMathContent()
    {
        var markdown = "Just a regular paragraph with no math.";
        var doc = ParseWithMath(markdown);
        var context = CreateContext();
        var transform = CreateTransform();

        transform.Transform(doc, context);

        // No math blocks or inlines, should not crash
        doc.Descendants<MathBlock>().ShouldBeEmpty();
        doc.Descendants<MathInline>().ShouldBeEmpty();
    }

    [Fact]
    public void Transform_HandlesMultipleMathExpressions()
    {
        // Single-line $$...$$ is MathInline with DelimiterCount=2
        // Multi-line $$ creates MathBlock
        var markdown = "Inline $a$ and $b$ plus display:\n\n$$\nc = a + b\n$$";
        var doc = ParseWithMath(markdown);
        var context = CreateContext();
        var transform = CreateTransform();

        transform.Transform(doc, context);

        var inlines = doc.Descendants<MathInline>().ToList();
        inlines.Count.ShouldBe(2);
        inlines.ShouldAllBe(i => !string.IsNullOrEmpty(i.GetOmmlXml()));

        var blocks = doc.Descendants<MathBlock>().ToList();
        blocks.Count.ShouldBe(1);
        blocks[0].GetOmmlXml().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Transform_ReturnsOriginalDocument()
    {
        var markdown = "$$x^2$$";
        var doc = ParseWithMath(markdown);
        var context = CreateContext();
        var transform = CreateTransform();

        var result = transform.Transform(doc, context);

        result.ShouldBeSameAs(doc);
    }

    [Fact]
    public void Name_ReturnsMathBlockAnnotator()
    {
        var transform = CreateTransform();
        transform.Name.ShouldBe("MathBlockAnnotator");
    }

    [Fact]
    public void Order_Returns35_BeforeMermaidAndSyntaxHighlighting()
    {
        var transform = CreateTransform();
        transform.Order.ShouldBe(35);
    }

    [Fact]
    public void Transform_GracefulDegradation_AddsWarningOnFailure()
    {
        // When Chromium is available, this succeeds — we verify no warnings added
        // When Chromium is unavailable, the transform should add a warning (not throw)
        var markdown = "$x^2$";
        var doc = ParseWithMath(markdown);
        var context = CreateContext();
        var transform = CreateTransform();

        transform.Transform(doc, context);

        var inline = doc.Descendants<MathInline>().SingleOrDefault();
        inline.ShouldNotBeNull();
        if (inline.GetOmmlXml() is not null)
        {
            // Success path: no warnings expected
            context.Warnings.ShouldBeEmpty();
        }
        else
        {
            // Degradation path: should have a warning
            context.Warnings.ShouldNotBeEmpty();
        }
    }
}
