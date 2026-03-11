// agent-notes: { ctx: "Issue 3 extension registration tests, TDD red", deps: [Md2.Parsing, Markdig], state: "red", last: "tara@2026-03-11" }

using Markdig;
using Markdig.Extensions.DefinitionLists;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.GenericAttributes;
using Markdig.Extensions.Mathematics;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Md2.Parsing;
using Shouldly;

namespace Md2.Parsing.Tests;

public class ExtensionRegistrationTests
{
    // ── Definition Lists ───────────────────────────────────────────────

    [Fact]
    public void Pipeline_ParsesDefinitionLists()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var markdown = "Term 1\n:   Definition 1\n\nTerm 2\n:   Definition 2";
        var doc = Markdown.Parse(markdown, pipeline);

        doc.Descendants<DefinitionList>().ShouldNotBeEmpty();
    }

    // ── Generic Attributes ─────────────────────────────────────────────

    [Fact]
    public void Pipeline_ParsesGenericAttributes()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var markdown = "# Heading {#my-id .my-class}";
        var doc = Markdown.Parse(markdown, pipeline);

        var heading = doc.OfType<HeadingBlock>().First();
        // Generic attributes are stored via TryGetAttributes
        var attrs = heading.TryGetAttributes();
        attrs.ShouldNotBeNull();
        attrs!.Id.ShouldBe("my-id");
    }

    // ── Math Expressions ───────────────────────────────────────────────

    [Fact]
    public void Pipeline_ParsesInlineMath()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var markdown = "Euler's formula: $e^{i\\pi} + 1 = 0$";
        var doc = Markdown.Parse(markdown, pipeline);

        var paragraph = doc.OfType<ParagraphBlock>().First();
        paragraph.Inline!.Descendants<MathInline>().ShouldNotBeEmpty();
    }

    [Fact]
    public void Pipeline_ParsesDisplayMath()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var markdown = "$$\n\\sum_{i=1}^{n} i = \\frac{n(n+1)}{2}\n$$";
        var doc = Markdown.Parse(markdown, pipeline);

        doc.Descendants<MathBlock>().ShouldNotBeEmpty();
    }

    // ── Footnotes ──────────────────────────────────────────────────────

    [Fact]
    public void Pipeline_ParsesFootnotes()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var markdown = "Text with a footnote[^1].\n\n[^1]: This is the footnote.";
        var doc = Markdown.Parse(markdown, pipeline);

        doc.Descendants<FootnoteGroup>().ShouldNotBeEmpty();
    }

    // ── Mermaid Code Blocks ────────────────────────────────────────────

    [Fact]
    public void Pipeline_IdentifiesMermaidCodeBlocks()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var markdown = "```mermaid\ngraph TD;\n    A-->B;\n```";
        var doc = Markdown.Parse(markdown, pipeline);

        var fenced = doc.OfType<FencedCodeBlock>().FirstOrDefault();
        fenced.ShouldNotBeNull();
        fenced!.Info.ShouldBe("mermaid");
    }

    [Fact]
    public void Pipeline_MermaidBlock_PreservesContent()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var mermaidSource = "graph TD;\n    A-->B;";
        var markdown = $"```mermaid\n{mermaidSource}\n```";
        var doc = Markdown.Parse(markdown, pipeline);

        var fenced = doc.OfType<FencedCodeBlock>().First();
        // Extract the text content from the code block lines
        var content = string.Join("\n", fenced.Lines.Lines
            .Where(l => l.Slice.Text != null)
            .Select(l => l.Slice.ToString()));
        content.ShouldContain("graph TD");
        content.ShouldContain("A-->B");
    }

    // ── All extensions register without conflict ───────────────────────

    [Fact]
    public void Pipeline_AllExtensionsCoexist_ComplexDocument()
    {
        var pipeline = Md2MarkdownPipeline.Build();
        var markdown = @"# Heading {#intro}

A paragraph with $E=mc^2$ math and a footnote[^1].

| Col A | Col B |
|-------|-------|
| 1     | 2     |

Term
:   Definition

```mermaid
graph TD;
    A-->B;
```

$$
\int_0^1 x^2 dx
$$

[^1]: A footnote.
";
        var doc = Markdown.Parse(markdown, pipeline);

        // All extension types should be present
        doc.OfType<HeadingBlock>().ShouldNotBeEmpty();
        doc.Descendants<MathInline>().ShouldNotBeEmpty();
        doc.Descendants<FootnoteGroup>().ShouldNotBeEmpty();
        doc.Descendants<DefinitionList>().ShouldNotBeEmpty();
        doc.OfType<FencedCodeBlock>().ShouldNotBeEmpty();
        doc.Descendants<MathBlock>().ShouldNotBeEmpty();
    }
}
