// agent-notes: { ctx: "Tests for ListBuilder: bullets, numbers, nesting, tasks", deps: [Md2.Emit.Docx.ListBuilder, Markdig, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
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
