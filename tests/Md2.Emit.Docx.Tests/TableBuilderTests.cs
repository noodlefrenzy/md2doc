// agent-notes: { ctx: "Tests for TableBuilder auto-sizing and structure", deps: [Md2.Emit.Docx.TableBuilder, Markdig.Extensions.Tables, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Shouldly;

using WTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using WTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using WTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using MdTable = Markdig.Extensions.Tables.Table;

namespace Md2.Emit.Docx.Tests;

public class TableBuilderTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();
    private const int DefaultAvailableWidth = 8306;

    private static MdTable ParseTable(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var table = document.Descendants<MdTable>().FirstOrDefault();
        if (table == null)
            throw new InvalidOperationException($"No table found in markdown. Parsed {document.Count} blocks: {string.Join(", ", document.Select(b => b.GetType().Name))}");
        return table;
    }

    [Fact]
    public void Build_UniformColumns_ColumnsRoughlyEqualWidth()
    {
        var md = "| AAA | BBB | CCC |\n|-----|-----|-----|\n| aaa | bbb | ccc |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var gridColumns = table.Descendants<GridColumn>().ToList();
        gridColumns.Count.ShouldBe(3);

        var widths = gridColumns.Select(gc => int.Parse(gc.Width!.Value!)).ToList();
        var avgWidth = widths.Average();
        foreach (var w in widths)
        {
            Math.Abs(w - avgWidth).ShouldBeLessThan(avgWidth * 0.1 + 1);
        }
    }

    [Fact]
    public void Build_VaryingLengths_LongerColumnGetsMoreWidth()
    {
        var md = "| A | This is a much longer column of text |\n|---|--------------------------------------|\n| B | Another fairly long piece of content |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var gridColumns = table.Descendants<GridColumn>().ToList();
        gridColumns.Count.ShouldBe(2);

        var widths = gridColumns.Select(gc => int.Parse(gc.Width!.Value!)).ToList();
        widths[1].ShouldBeGreaterThan(widths[0]);
    }

    [Fact]
    public void Build_WithHeader_FirstRowHasHeaderShading()
    {
        var md = "| Header1 | Header2 |\n|---------|---------|-----|\n| data1   | data2   |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var rows = table.Descendants<WTableRow>().ToList();
        rows.Count.ShouldBeGreaterThanOrEqualTo(2);

        var headerCells = rows[0].Descendants<WTableCell>().ToList();
        headerCells.ShouldNotBeEmpty();
        foreach (var cell in headerCells)
        {
            var shading = cell.TableCellProperties?.Shading;
            shading.ShouldNotBeNull();
            shading!.Fill!.Value.ShouldBe(_theme.TableHeaderBackground);
        }
    }

    [Fact]
    public void Build_VeryLongCell_ColumnCappedAtMaxWidth()
    {
        var longText = new string('x', 250);
        var dashes = new string('-', 250);
        var md = $"| Short | {longText} |\n|-------|{dashes}|\n| a     | b          |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var gridColumns = table.Descendants<GridColumn>().ToList();
        var widths = gridColumns.Select(gc => int.Parse(gc.Width!.Value!)).ToList();

        var maxAllowed = (int)(DefaultAvailableWidth * 0.60);
        foreach (var w in widths)
        {
            w.ShouldBeLessThanOrEqualTo(maxAllowed);
        }
    }

    [Fact]
    public void Build_Simple2x2_ProducesValidTableElement()
    {
        var md = "| A | B |\n|---|---|\n| 1 | 2 |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        table.ShouldNotBeNull();
        table.ShouldBeOfType<WTable>();
        table.GetFirstChild<TableProperties>().ShouldNotBeNull();
        table.GetFirstChild<TableGrid>().ShouldNotBeNull();

        var rows = table.Elements<WTableRow>().ToList();
        rows.Count.ShouldBe(2);

        foreach (var row in rows)
        {
            row.Elements<WTableCell>().Count().ShouldBe(2);
        }
    }

    [Fact]
    public void Build_EmptyCells_DoesNotThrow()
    {
        var md = "| A |   |\n|---|---|\n|   | B |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        table.ShouldNotBeNull();
        var rows = table.Elements<WTableRow>().ToList();
        rows.Count.ShouldBe(2);

        foreach (var row in rows)
        {
            foreach (var cell in row.Elements<WTableCell>())
            {
                cell.Elements<Paragraph>().ShouldNotBeEmpty();
            }
        }
    }

    [Fact]
    public void Build_SingleColumn_ProducesValidTable()
    {
        var md = "| Only |\n|------|\n| one  |\n| col  |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var gridColumns = table.Descendants<GridColumn>().ToList();
        gridColumns.Count.ShouldBe(1);

        var rows = table.Elements<WTableRow>().ToList();
        rows.Count.ShouldBe(3);
    }

    [Fact]
    public void Build_ColumnWidths_SumToAvailableWidth()
    {
        var md = "| Col1 | Col2 | Col3 | Col4 |\n|------|------|------|------|\n| a    | bb   | ccc  | dddd |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var gridColumns = table.Descendants<GridColumn>().ToList();
        gridColumns.Count.ShouldBe(4);

        var totalWidth = gridColumns.Sum(gc => int.Parse(gc.Width!.Value!));
        totalWidth.ShouldBe(DefaultAvailableWidth);
    }

    [Fact]
    public void Build_TableLayout_IsFixed()
    {
        var md = "| A | B |\n|---|---|\n| 1 | 2 |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var layout = table.GetFirstChild<TableProperties>()?.TableLayout;
        layout.ShouldNotBeNull();
        layout!.Type!.Value.ShouldBe(TableLayoutValues.Fixed);
    }

    [Fact]
    public void Build_ManyColumns_NoColumnBelowMinWidth()
    {
        var md = "| A | B | C | D | E | F | G | H | I | J |\n|---|---|---|---|---|---|---|---|---|---|\n| 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 0 |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var gridColumns = table.Descendants<GridColumn>().ToList();
        var minAllowed = (int)(DefaultAvailableWidth * 0.05);

        foreach (var gc in gridColumns)
        {
            int.Parse(gc.Width!.Value!).ShouldBeGreaterThanOrEqualTo(minAllowed);
        }
    }

    [Fact]
    public void Build_CellContent_ContainsParagraphWithText()
    {
        var md = "| Hello | World |\n|-------|-------|\n| Foo   | Bar   |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var firstCell = table.Descendants<WTableCell>().First();
        var paragraph = firstCell.GetFirstChild<Paragraph>();
        paragraph.ShouldNotBeNull();

        var text = string.Join("", paragraph!.Descendants<Text>().Select(t => t.Text));
        text.ShouldNotBeNullOrWhiteSpace();
    }
}
