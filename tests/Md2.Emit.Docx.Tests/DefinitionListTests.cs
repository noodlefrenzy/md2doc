// agent-notes: { ctx: "Tests for definition list rendering", deps: [Md2.Emit.Docx, DocumentFormat.OpenXml, Markdig], state: active, last: "tara@2026-03-12" }

using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Md2.Parsing;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class DefinitionListTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();

    private MarkdownDocument Parse(string markdown)
    {
        var pipeline = Md2MarkdownPipeline.Build(new ParserOptions());
        return Markdown.Parse(markdown, pipeline);
    }

    [Fact]
    public void DefinitionList_TermIsBold()
    {
        var doc = Parse("Term\n:   Definition text");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var paragraphs = elements.OfType<Paragraph>().ToList();
        paragraphs.ShouldNotBeEmpty();
        // Term paragraph should have bold run
        var termPara = paragraphs.First();
        termPara.Descendants<Run>().Any(r => r.RunProperties?.Bold != null).ShouldBeTrue();
    }

    [Fact]
    public void DefinitionList_DefinitionIsIndented()
    {
        var doc = Parse("Term\n:   Definition text");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var paragraphs = elements.OfType<Paragraph>().ToList();
        paragraphs.Count.ShouldBeGreaterThanOrEqualTo(2);
        // Definition paragraph should be indented
        var defPara = paragraphs[1];
        var indent = defPara.ParagraphProperties?.Indentation;
        indent.ShouldNotBeNull();
        int.Parse(indent!.Left!.Value!).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void DefinitionList_MultipleDefinitions_AllIndented()
    {
        var doc = Parse("Term\n:   First definition\n:   Second definition");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var paragraphs = elements.OfType<Paragraph>().ToList();
        paragraphs.Count.ShouldBeGreaterThanOrEqualTo(3);

        // Skip term, check definitions are indented
        for (var i = 1; i < paragraphs.Count; i++)
        {
            var indent = paragraphs[i].ParagraphProperties?.Indentation;
            indent.ShouldNotBeNull($"Definition paragraph {i} should be indented");
        }
    }

    [Fact]
    public void DefinitionList_MultipleTerms_AllBold()
    {
        var doc = Parse("Term One\n:   Definition one\n\nTerm Two\n:   Definition two");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var boldParas = elements.OfType<Paragraph>()
            .Where(p => p.Descendants<Run>().Any(r => r.RunProperties?.Bold != null))
            .ToList();

        boldParas.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void DefinitionList_ContainsExpectedText()
    {
        var doc = Parse("Apple\n:   A fruit that grows on trees");
        var visitor = TestHelper.CreateVisitor(_theme);

        var elements = visitor.Visit(doc).ToList();

        var allText = string.Join(" ", elements.SelectMany(e => e.Descendants<Text>()).Select(t => t.Text));
        allText.ShouldContain("Apple");
        allText.ShouldContain("fruit");
    }
}
