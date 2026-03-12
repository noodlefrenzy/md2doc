// agent-notes: { ctx: "Tests for SyntaxHighlightAnnotator transform", deps: [Md2.Highlight.SyntaxHighlightAnnotator, Md2.Core], state: active, last: "tara@2026-03-12" }

using Markdig;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Highlight;
using Md2.Parsing;
using Shouldly;

namespace Md2.Highlight.Tests;

public class SyntaxHighlightAnnotatorTests
{
    [Fact]
    public void Transform_FencedCodeBlock_AttachesSyntaxTokens()
    {
        var markdown = "```csharp\nvar x = 42;\n```";
        var pipeline = Md2MarkdownPipeline.Build(new ParserOptions());
        var doc = Markdown.Parse(markdown, pipeline);
        var annotator = new SyntaxHighlightAnnotator();
        var context = new TransformContext(new DocumentMetadata(), new TransformOptions());

        annotator.Transform(doc, context);

        var codeBlock = doc.Descendants<FencedCodeBlock>().First();
        var tokens = codeBlock.GetSyntaxTokens();
        tokens.ShouldNotBeNull();
        tokens!.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Transform_FencedCodeBlockNoLanguage_NoTokensAttached()
    {
        var markdown = "```\nplain code\n```";
        var pipeline = Md2MarkdownPipeline.Build(new ParserOptions());
        var doc = Markdown.Parse(markdown, pipeline);
        var annotator = new SyntaxHighlightAnnotator();
        var context = new TransformContext(new DocumentMetadata(), new TransformOptions());

        annotator.Transform(doc, context);

        var codeBlock = doc.Descendants<FencedCodeBlock>().First();
        var tokens = codeBlock.GetSyntaxTokens();
        tokens.ShouldBeNull();
    }

    [Fact]
    public void Transform_MultipleCodeBlocks_AllAnnotated()
    {
        var markdown = "```python\nprint(1)\n```\n\n```js\nconst x = 1;\n```";
        var pipeline = Md2MarkdownPipeline.Build(new ParserOptions());
        var doc = Markdown.Parse(markdown, pipeline);
        var annotator = new SyntaxHighlightAnnotator();
        var context = new TransformContext(new DocumentMetadata(), new TransformOptions());

        annotator.Transform(doc, context);

        var codeBlocks = doc.Descendants<FencedCodeBlock>().ToList();
        codeBlocks.Count.ShouldBe(2);
        codeBlocks[0].GetSyntaxTokens().ShouldNotBeNull();
        codeBlocks[1].GetSyntaxTokens().ShouldNotBeNull();
    }

    [Fact]
    public void Order_Is50()
    {
        var annotator = new SyntaxHighlightAnnotator();
        annotator.Order.ShouldBe(50);
    }
}
