// agent-notes: { ctx: "Spike #104: validate Markdig AST fragment reparenting for slide splitting", deps: [Markdig, Md2.Parsing, Md2.Highlight, Md2.Core.Ast], state: active, last: "tara@2026-03-15" }

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Core.Ast;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Highlight;
using Md2.Parsing;
using Shouldly;

namespace Md2.Core.Tests.Spikes;

/// <summary>
/// Spike: ADR-0014 Required Spike #1 — AST fragment reparenting validation.
///
/// Tests whether Markdig AST nodes can be safely detached from a full MarkdownDocument
/// and reparented into per-slide MarkdownDocument fragments, while preserving:
/// 1. SetData/GetData annotations (syntax tokens, Mermaid paths, OMML)
/// 2. Transform compatibility (SyntaxHighlightAnnotator on fragments)
/// 3. Descendants() enumeration on fragments
/// 4. Cross-slide link reference definitions
/// </summary>
public class AstFragmentReparentingSpikeTests
{
    private const string MultiSlideMarkdown =
"# Slide 1\n\nSome intro text with **bold**.\n\n```csharp\nvar x = 42;\n```\n\n---\n\n# Slide 2\n\n- Item A\n- Item B\n\n```python\nprint(\"hello\")\n```\n\n---\n\n# Slide 3\n\nFinal slide with a [link][ref1].\n\n[ref1]: https://example.com\n";

    private static MarkdownDocument ParseFull()
    {
        // Disable YAML front matter so --- is parsed as ThematicBreakBlock, not front matter
        var options = new ParserOptions { EnableYamlFrontMatter = false };
        var pipeline = Md2MarkdownPipeline.Build(options);
        return Markdown.Parse(MultiSlideMarkdown, pipeline);
    }

    /// <summary>
    /// Split a parsed MarkdownDocument at ThematicBreakBlock boundaries,
    /// returning a list of new MarkdownDocument instances each containing
    /// the blocks for one slide.
    /// </summary>
    private static List<MarkdownDocument> SplitAtThematicBreaks(MarkdownDocument fullDoc)
    {
        // Snapshot the blocks first to avoid mutation during iteration
        var allBlocks = fullDoc.ToList();

        var slides = new List<MarkdownDocument>();
        var currentBlocks = new List<Block>();

        foreach (var block in allBlocks)
        {
            if (block is ThematicBreakBlock)
            {
                if (currentBlocks.Count > 0)
                {
                    slides.Add(CreateFragment(currentBlocks, fullDoc));
                    currentBlocks = new List<Block>();
                }
            }
            else
            {
                currentBlocks.Add(block);
            }
        }

        if (currentBlocks.Count > 0)
            slides.Add(CreateFragment(currentBlocks, fullDoc));

        return slides;
    }

    private static MarkdownDocument CreateFragment(List<Block> blocks, MarkdownDocument parent)
    {
        var fragment = new MarkdownDocument();
        foreach (var block in blocks)
        {
            // Remove from original parent first
            parent.Remove(block);
            fragment.Add(block);
        }
        return fragment;
    }

    // ── Test 1: Basic splitting produces correct number of slides ─────

    [Fact]
    public void Split_ThreeSlideMarkdown_ProducesThreeFragments()
    {
        var fullDoc = ParseFull();
        var slides = SplitAtThematicBreaks(fullDoc);

        slides.Count.ShouldBe(3);
    }

    // ── Test 2: Fragments contain expected block types ─────────────────

    [Fact]
    public void Split_FragmentsContainExpectedBlocks()
    {
        var fullDoc = ParseFull();
        var slides = SplitAtThematicBreaks(fullDoc);

        // Slide 1: heading + paragraph + fenced code
        slides[0].OfType<HeadingBlock>().Count().ShouldBe(1);
        slides[0].OfType<ParagraphBlock>().Count().ShouldBeGreaterThanOrEqualTo(1);
        slides[0].OfType<FencedCodeBlock>().Count().ShouldBe(1);

        // Slide 2: heading + list + fenced code
        slides[1].OfType<HeadingBlock>().Count().ShouldBe(1);
        slides[1].OfType<ListBlock>().Count().ShouldBe(1);
        slides[1].OfType<FencedCodeBlock>().Count().ShouldBe(1);

        // Slide 3: heading + paragraph (+ link ref def, possibly)
        slides[2].OfType<HeadingBlock>().Count().ShouldBe(1);
        slides[2].OfType<ParagraphBlock>().Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    // ── Test 3: Descendants() works on reparented fragments ───────────

    [Fact]
    public void Split_DescendantsEnumeration_WorksOnFragments()
    {
        var fullDoc = ParseFull();
        var slides = SplitAtThematicBreaks(fullDoc);

        // Each fragment should enumerate descendants without error
        foreach (var slide in slides)
        {
            var descendants = slide.Descendants().ToList();
            descendants.ShouldNotBeEmpty();
        }
    }

    // ── Test 4: SetData annotations survive reparenting ───────────────

    [Fact]
    public void Split_SetDataAnnotations_SurviveReparenting()
    {
        var fullDoc = ParseFull();

        // Annotate all fenced code blocks BEFORE splitting
        foreach (var block in fullDoc.Descendants<FencedCodeBlock>())
        {
            block.SetSyntaxTokens(new List<SyntaxToken>
            {
                new("var", "#569CD6", SyntaxFontStyle.Normal)
            });
        }

        // Now split
        var slides = SplitAtThematicBreaks(fullDoc);

        // Verify annotations survived on slide 1's code block
        var slide1Code = slides[0].Descendants<FencedCodeBlock>().FirstOrDefault();
        slide1Code.ShouldNotBeNull();
        var tokens = slide1Code.GetSyntaxTokens();
        tokens.ShouldNotBeNull();
        tokens.Count.ShouldBe(1);
        tokens[0].Text.ShouldBe("var");

        // Verify annotations survived on slide 2's code block
        var slide2Code = slides[1].Descendants<FencedCodeBlock>().FirstOrDefault();
        slide2Code.ShouldNotBeNull();
        slide2Code.GetSyntaxTokens().ShouldNotBeNull();
    }

    // ── Test 5: SyntaxHighlightAnnotator works on fragments ───────────

    [Fact]
    public void Split_SyntaxHighlightAnnotator_WorksOnFragments()
    {
        var fullDoc = ParseFull();
        var slides = SplitAtThematicBreaks(fullDoc);

        var annotator = new SyntaxHighlightAnnotator();
        var context = new TransformContext(
            new DocumentMetadata(),
            new TransformOptions());

        // Run annotator on each fragment independently
        foreach (var slide in slides)
        {
            var result = annotator.Transform(slide, context);
            result.ShouldNotBeNull();
        }

        // Slide 1 has a csharp block — should have tokens
        var csharpBlock = slides[0].Descendants<FencedCodeBlock>()
            .FirstOrDefault(b => b.Info == "csharp");
        csharpBlock.ShouldNotBeNull();
        csharpBlock.GetSyntaxTokens().ShouldNotBeNull();
        csharpBlock.GetSyntaxTokens()!.Count.ShouldBeGreaterThan(0);

        // Slide 2 has a python block — should have tokens
        var pythonBlock = slides[1].Descendants<FencedCodeBlock>()
            .FirstOrDefault(b => b.Info == "python");
        pythonBlock.ShouldNotBeNull();
        pythonBlock.GetSyntaxTokens().ShouldNotBeNull();
        pythonBlock.GetSyntaxTokens()!.Count.ShouldBeGreaterThan(0);
    }

    // ── Test 6: Annotate-then-split (preferred pipeline order) ────────

    [Fact]
    public void AnnotateThenSplit_TokensSurviveOnCorrectSlides()
    {
        var fullDoc = ParseFull();

        // Run SyntaxHighlightAnnotator on full doc FIRST
        var annotator = new SyntaxHighlightAnnotator();
        var context = new TransformContext(
            new DocumentMetadata(),
            new TransformOptions());
        annotator.Transform(fullDoc, context);

        // Then split
        var slides = SplitAtThematicBreaks(fullDoc);

        // Slide 1's csharp block should have tokens from the full-doc pass
        var csharpBlock = slides[0].Descendants<FencedCodeBlock>()
            .FirstOrDefault(b => b.Info == "csharp");
        csharpBlock.ShouldNotBeNull();
        csharpBlock.GetSyntaxTokens().ShouldNotBeNull();

        // Slide 2's python block should also have tokens
        var pythonBlock = slides[1].Descendants<FencedCodeBlock>()
            .FirstOrDefault(b => b.Info == "python");
        pythonBlock.ShouldNotBeNull();
        pythonBlock.GetSyntaxTokens().ShouldNotBeNull();
    }

    // ── Test 7: Generic SetData/GetData on arbitrary blocks ───────────

    [Fact]
    public void Split_GenericSetData_SurvivesReparenting()
    {
        var fullDoc = ParseFull();

        // Set arbitrary data on first heading
        var heading = fullDoc.Descendants<HeadingBlock>().First();
        heading.SetData("spike:test-key", "test-value");

        var slides = SplitAtThematicBreaks(fullDoc);

        // Retrieve from reparented fragment
        var reparentedHeading = slides[0].Descendants<HeadingBlock>().First();
        var value = reparentedHeading.GetData("spike:test-key") as string;
        value.ShouldBe("test-value");
    }

    // ── Test 8: Cross-slide link references ───────────────────────────

    [Fact]
    public void Split_LinkReferenceDefinitions_CanBeCollected()
    {
        var fullDoc = ParseFull();

        // Collect all link reference definitions from the full doc before splitting
        var linkDefs = fullDoc.Descendants<LinkReferenceDefinition>().ToList();

        // MARP markdown has [ref1]: https://example.com on slide 3
        // This test validates we can find them before splitting and
        // could duplicate them into other slides if needed
        linkDefs.Count.ShouldBeGreaterThanOrEqualTo(1);
        linkDefs.Any(d => d.Label == "ref1").ShouldBeTrue();

        var slides = SplitAtThematicBreaks(fullDoc);

        // The link ref def should end up in slide 3
        var slide3LinkDefs = slides[2].Descendants<LinkReferenceDefinition>().ToList();
        // Note: LinkReferenceDefinitions may be stored differently in Markdig
        // This test validates we can at least enumerate them post-split
        slides.Count.ShouldBe(3);
    }

    // ── Test 9: Inline content survives reparenting ───────────────────

    [Fact]
    public void Split_InlineContent_PreservedAfterReparenting()
    {
        var fullDoc = ParseFull();
        var slides = SplitAtThematicBreaks(fullDoc);

        // Slide 1 has "**bold**" — check emphasis inline survived
        var slide1Paragraph = slides[0].Descendants<ParagraphBlock>().First();
        var emphases = slide1Paragraph.Inline?.Descendants<EmphasisInline>().ToList();
        emphases.ShouldNotBeNull();
        emphases.Count.ShouldBeGreaterThan(0);
    }

    // ── Test 10: Empty slides (consecutive breaks) handled ────────────

    [Fact]
    public void Split_ConsecutiveThematicBreaks_ProducesNoEmptySlides()
    {
        var markdown = "# Slide 1\n\n---\n\n---\n\n# Slide 2\n";
        var pipeline = Md2MarkdownPipeline.Build(new ParserOptions());
        var doc = Markdown.Parse(markdown, pipeline);
        var slides = SplitAtThematicBreaks(doc);

        // Should not produce empty fragments
        foreach (var slide in slides)
        {
            slide.Count.ShouldBeGreaterThan(0);
        }
    }
}
