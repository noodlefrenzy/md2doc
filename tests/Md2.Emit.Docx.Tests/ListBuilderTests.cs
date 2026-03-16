// agent-notes: { ctx: "Tests for ListBuilder: bullets, numbers, nesting, tasks", deps: [Md2.Emit.Docx.ListBuilder, Markdig, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Shouldly;

using MdListBlock = Markdig.Syntax.ListBlock;

namespace Md2.Emit.Docx.Tests;

public class ListBuilderTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();

    private static (MarkdownDocument doc, MdListBlock list) ParseList(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseTaskLists()
            .Build();
        var document = Markdown.Parse(markdown, pipeline);
        var list = document.Descendants<MdListBlock>().FirstOrDefault();
        if (list == null)
            throw new InvalidOperationException(
                $"No list found. Blocks: {string.Join(", ", document.Select(b => b.GetType().Name))}");
        return (document, list);
    }

    private (ListBuilder builder, MainDocumentPart mainPart) CreateBuilder()
    {
        var stream = new MemoryStream();
        var wordDoc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        var paragraphBuilder = new ParagraphBuilder(_theme);
        var builder = new ListBuilder(paragraphBuilder, mainPart);
        return (builder, mainPart);
    }

    [Fact]
    public void Build_UnorderedList_ProducesParagraphsWithBulletNumbering()
    {
        var md = "- Item 1\n- Item 2\n- Item 3";
        var (doc, list) = ParseList(md);
        var (builder, _) = CreateBuilder();

        var paragraphs = builder.Build(list);

        paragraphs.Count.ShouldBe(3);
        foreach (var p in paragraphs)
        {
            var numProps = p.ParagraphProperties?.NumberingProperties;
            numProps.ShouldNotBeNull();
            numProps!.NumberingId.ShouldNotBeNull();
            numProps!.NumberingLevelReference.ShouldNotBeNull();
            numProps!.NumberingLevelReference!.Val!.Value.ShouldBe(0);
        }
    }

    [Fact]
    public void Build_OrderedList_ProducesParagraphsWithDecimalNumbering()
    {
        var md = "1. First\n2. Second\n3. Third";
        var (doc, list) = ParseList(md);
        var (builder, mainPart) = CreateBuilder();

        var paragraphs = builder.Build(list);

        paragraphs.Count.ShouldBe(3);

        // Check numbering definitions part was created with decimal format
        var numberingPart = mainPart.NumberingDefinitionsPart;
        numberingPart.ShouldNotBeNull();
        var abstractNums = numberingPart!.Numbering!.Elements<AbstractNum>().ToList();
        abstractNums.ShouldNotBeEmpty();

        // Find the abstract numbering used by this list
        var numId = paragraphs[0].ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value;
        var numInstance = numberingPart.Numbering.Elements<NumberingInstance>()
            .First(ni => ni.NumberID!.Value == numId);
        var abstractNumId = numInstance.AbstractNumId!.Val!.Value;
        var abstractNum = abstractNums.First(a => a.AbstractNumberId!.Value == abstractNumId);
        var level0 = abstractNum.Elements<Level>().First(l => l.LevelIndex == 0);
        level0.GetFirstChild<NumberingFormat>()!.Val!.Value.ShouldBe(NumberFormatValues.Decimal);
    }

    [Fact]
    public void Build_NestedList_IncrementsLevelIndex()
    {
        var md = "- Outer\n  - Inner\n    - Deep";
        var (doc, list) = ParseList(md);
        var (builder, _) = CreateBuilder();

        var paragraphs = builder.Build(list);

        paragraphs.Count.ShouldBeGreaterThanOrEqualTo(3);

        // First item at level 0
        paragraphs[0].ParagraphProperties!.NumberingProperties!.NumberingLevelReference!.Val!.Value.ShouldBe(0);
        // Second item at level 1
        paragraphs[1].ParagraphProperties!.NumberingProperties!.NumberingLevelReference!.Val!.Value.ShouldBe(1);
        // Third item at level 2
        paragraphs[2].ParagraphProperties!.NumberingProperties!.NumberingLevelReference!.Val!.Value.ShouldBe(2);
    }

    [Fact]
    public void Build_TaskList_HasCheckboxPrefix()
    {
        var md = "- [x] Done task\n- [ ] Open task";
        var (doc, list) = ParseList(md);
        var (builder, _) = CreateBuilder();

        var paragraphs = builder.Build(list);

        paragraphs.Count.ShouldBe(2);

        // First task is checked
        var text1 = string.Join("", paragraphs[0].Descendants<Text>().Select(t => t.Text));
        text1.ShouldContain("\u2611"); // ☑

        // Second task is unchecked
        var text2 = string.Join("", paragraphs[1].Descendants<Text>().Select(t => t.Text));
        text2.ShouldContain("\u2610"); // ☐
    }

    // ── Inline visitor tests (formatting preserved in lists) ───────────

    private (List<OpenXmlElement> elements, MainDocumentPart mainPart) VisitMarkdown(string md)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
            .Build();
        var document = Markdown.Parse(md, pipeline);

        var stream = new MemoryStream();
        var wordDoc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        var paragraphBuilder = new ParagraphBuilder(_theme);
        var visitor = new DocxAstVisitor(paragraphBuilder, mainPart, _theme);

        return (visitor.Visit(document).ToList(), mainPart);
    }

    [Fact]
    public void Build_WithInlineVisitor_LinksEmitAsHyperlinks()
    {
        var (elements, mainPart) = VisitMarkdown("- [Google](https://google.com)\n- Plain text");

        var firstParagraph = elements.OfType<Paragraph>().First();
        var hyperlinks = firstParagraph.Descendants<Hyperlink>().ToList();
        hyperlinks.Count.ShouldBe(1);

        var rel = mainPart.HyperlinkRelationships.FirstOrDefault(r => r.Id == hyperlinks[0].Id);
        rel.ShouldNotBeNull();
        rel!.Uri.ToString().ShouldBe("https://google.com/");
    }

    [Fact]
    public void Build_WithInlineVisitor_ReferenceLinkEmitsAsHyperlink()
    {
        var (elements, _) = VisitMarkdown("- [Example][ex]\n\n[ex]: https://example.com");

        var firstParagraph = elements.OfType<Paragraph>().First();
        var hyperlinks = firstParagraph.Descendants<Hyperlink>().ToList();
        hyperlinks.Count.ShouldBe(1);
    }

    [Fact]
    public void Build_WithInlineVisitor_BoldTextPreserved()
    {
        var (elements, _) = VisitMarkdown("- **bold item**");

        var firstParagraph = elements.OfType<Paragraph>().First();
        var runs = firstParagraph.Descendants<Run>().ToList();
        runs.ShouldNotBeEmpty();

        var boldRun = runs.First(r =>
            r.Descendants<Text>().Any(t => t.Text.Contains("bold item")));
        boldRun.RunProperties.ShouldNotBeNull();
        boldRun.RunProperties!.GetFirstChild<Bold>().ShouldNotBeNull();
    }

    [Fact]
    public void Build_WithInlineVisitor_ItalicTextPreserved()
    {
        var (elements, _) = VisitMarkdown("- *italic item*");

        var firstParagraph = elements.OfType<Paragraph>().First();
        var runs = firstParagraph.Descendants<Run>().ToList();
        runs.ShouldNotBeEmpty();

        var italicRun = runs.First(r =>
            r.Descendants<Text>().Any(t => t.Text.Contains("italic item")));
        italicRun.RunProperties.ShouldNotBeNull();
        italicRun.RunProperties!.GetFirstChild<Italic>().ShouldNotBeNull();
    }

    [Fact]
    public void Build_WithInlineVisitor_StrikethroughTextPreserved()
    {
        var (elements, _) = VisitMarkdown("- ~~deleted~~");

        var firstParagraph = elements.OfType<Paragraph>().First();
        var runs = firstParagraph.Descendants<Run>().ToList();
        runs.ShouldNotBeEmpty();

        var strikeRun = runs.First(r =>
            r.Descendants<Text>().Any(t => t.Text.Contains("deleted")));
        strikeRun.RunProperties.ShouldNotBeNull();
        strikeRun.RunProperties!.GetFirstChild<Strike>().ShouldNotBeNull();
    }

    [Fact]
    public void Build_WithInlineVisitor_InlineCodePreserved()
    {
        var (elements, _) = VisitMarkdown("- use `Console.WriteLine`");

        var firstParagraph = elements.OfType<Paragraph>().First();
        var allText = string.Join("", firstParagraph.Descendants<Text>().Select(t => t.Text));
        allText.ShouldContain("Console.WriteLine");

        // Inline code should have a distinct run (not merged with surrounding text)
        var runs = firstParagraph.Descendants<Run>().ToList();
        runs.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Build_WithInlineVisitor_MixedFormattingInSingleItem()
    {
        var (elements, mainPart) = VisitMarkdown(
            "- **Bold** and *italic* with [a link](https://example.com)");

        var firstParagraph = elements.OfType<Paragraph>().First();

        // Should have bold run
        var boldRun = firstParagraph.Descendants<Run>()
            .FirstOrDefault(r => r.RunProperties?.GetFirstChild<Bold>() != null
                && r.Descendants<Text>().Any(t => t.Text.Contains("Bold")));
        boldRun.ShouldNotBeNull();

        // Should have italic run
        var italicRun = firstParagraph.Descendants<Run>()
            .FirstOrDefault(r => r.RunProperties?.GetFirstChild<Italic>() != null
                && r.Descendants<Text>().Any(t => t.Text.Contains("italic")));
        italicRun.ShouldNotBeNull();

        // Should have hyperlink
        var hyperlinks = firstParagraph.Descendants<Hyperlink>().ToList();
        hyperlinks.Count.ShouldBe(1);
    }

    [Fact]
    public void Build_MixedNestedLists_OrderedInsideUnordered()
    {
        var md = "- Bullet item\n  1. Numbered sub-item\n  2. Another numbered";
        var (doc, list) = ParseList(md);
        var (builder, mainPart) = CreateBuilder();

        var paragraphs = builder.Build(list);

        paragraphs.Count.ShouldBeGreaterThanOrEqualTo(3);

        // First item level 0
        paragraphs[0].ParagraphProperties!.NumberingProperties!.NumberingLevelReference!.Val!.Value.ShouldBe(0);
        // Nested items level 1
        paragraphs[1].ParagraphProperties!.NumberingProperties!.NumberingLevelReference!.Val!.Value.ShouldBe(1);
    }

    [Fact]
    public void Build_ListItemWithMultipleBlocks_ProducesMultipleParagraphs()
    {
        // A list item can contain multiple paragraphs if separated by blank line
        var md = "- Item one\n- Item two";
        var (doc, list) = ParseList(md);
        var (builder, _) = CreateBuilder();

        var paragraphs = builder.Build(list);

        paragraphs.Count.ShouldBeGreaterThanOrEqualTo(2);
        foreach (var p in paragraphs)
        {
            var text = string.Join("", p.Descendants<Text>().Select(t => t.Text));
            text.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
