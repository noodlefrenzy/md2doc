// agent-notes: { ctx: "Tests for table styling: borders, headers, alternating rows, padding", deps: [Md2.Emit.Docx.TableBuilder, Markdig.Extensions.Tables, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

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

public class TableStylingTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();
    private const int DefaultAvailableWidth = 8306;

    private static MdTable ParseTable(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var table = document.Descendants<MdTable>().FirstOrDefault();
        if (table == null)
            throw new InvalidOperationException("No table found in markdown.");
        return table;
    }

    [Fact]
    public void Build_HeaderRow_HasTableHeaderProperty()
    {
        var md = "| H1 | H2 |\n|----|----|\n| a  | b  |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var rows = table.Elements<WTableRow>().ToList();
        rows.Count.ShouldBeGreaterThanOrEqualTo(2);

        // Header row should have TableHeader property for repeat on page break
        var headerRowProps = rows[0].GetFirstChild<TableRowProperties>();
        headerRowProps.ShouldNotBeNull();
        headerRowProps!.GetFirstChild<TableHeader>().ShouldNotBeNull();
    }

    [Fact]
    public void Build_AlternatingRowShading_AppliedToEvenDataRows()
    {
        var md = "| H1 | H2 |\n|----|----|\n| r1 | r1 |\n| r2 | r2 |\n| r3 | r3 |\n| r4 | r4 |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var rows = table.Elements<WTableRow>().ToList();
        // rows[0] = header, rows[1..4] = data rows (indices 0-3)
        // Even data rows (data index 1, 3 => rows[2], rows[4]) get shading
        rows.Count.ShouldBe(5);

        // Data row index 1 (second data row, rows[2]) should have alternate shading
        var evenDataCells = rows[2].Descendants<WTableCell>().ToList();
        foreach (var cell in evenDataCells)
        {
            var shading = cell.TableCellProperties?.Shading;
            shading.ShouldNotBeNull();
            shading!.Fill!.Value.ShouldBe(_theme.TableAlternateRowBackground);
        }

        // Data row index 3 (fourth data row, rows[4]) should also have alternate shading
        var evenDataCells2 = rows[4].Descendants<WTableCell>().ToList();
        foreach (var cell in evenDataCells2)
        {
            var shading = cell.TableCellProperties?.Shading;
            shading.ShouldNotBeNull();
            shading!.Fill!.Value.ShouldBe(_theme.TableAlternateRowBackground);
        }
    }

    [Fact]
    public void Build_OddDataRows_DoNotHaveAlternateShading()
    {
        var md = "| H1 | H2 |\n|----|----|\n| r1 | r1 |\n| r2 | r2 |\n| r3 | r3 |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var rows = table.Elements<WTableRow>().ToList();
        // rows[1] = first data row (odd, index 0), rows[3] = third data row (odd, index 2)
        // These should NOT have alternate row background
        var firstDataCells = rows[1].Descendants<WTableCell>().ToList();
        foreach (var cell in firstDataCells)
        {
            var shading = cell.TableCellProperties?.Shading;
            // Either no shading or shading fill is not the alternate background
            if (shading != null && shading.Fill != null)
            {
                shading.Fill.Value.ShouldNotBe(_theme.TableAlternateRowBackground);
            }
        }
    }

    [Fact]
    public void Build_CellPadding_IsSet()
    {
        var md = "| H1 | H2 |\n|----|----|\n| a  | b  |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var cells = table.Descendants<WTableCell>().ToList();
        cells.ShouldNotBeEmpty();

        foreach (var cell in cells)
        {
            var margin = cell.TableCellProperties?.TableCellMargin;
            margin.ShouldNotBeNull();
            margin!.LeftMargin.ShouldNotBeNull();
            margin!.RightMargin.ShouldNotBeNull();
            margin!.TopMargin.ShouldNotBeNull();
            margin!.BottomMargin.ShouldNotBeNull();
        }
    }

    [Fact]
    public void Build_TableBorders_UseThemeColors()
    {
        var md = "| H1 | H2 |\n|----|----|\n| a  | b  |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var borders = table.GetFirstChild<TableProperties>()?.TableBorders;
        borders.ShouldNotBeNull();
        borders!.TopBorder!.Color!.Value.ShouldBe(_theme.TableBorderColor);
        borders!.BottomBorder!.Color!.Value.ShouldBe(_theme.TableBorderColor);
        borders!.LeftBorder!.Color!.Value.ShouldBe(_theme.TableBorderColor);
        borders!.RightBorder!.Color!.Value.ShouldBe(_theme.TableBorderColor);
    }

    [Fact]
    public void Build_NonHeaderRows_DoNotHaveHeaderShading()
    {
        var md = "| H1 | H2 |\n|----|----|\n| a  | b  |\n| c  | d  |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var rows = table.Elements<WTableRow>().ToList();

        // Data rows should NOT have header background
        for (int i = 1; i < rows.Count; i++)
        {
            var cells = rows[i].Descendants<WTableCell>().ToList();
            foreach (var cell in cells)
            {
                var shading = cell.TableCellProperties?.Shading;
                if (shading != null && shading.Fill != null)
                {
                    shading.Fill.Value.ShouldNotBe(_theme.TableHeaderBackground);
                }
            }
        }
    }

    [Fact]
    public void Build_NonHeaderRows_AllowPageSplit()
    {
        var md = "| H1 | H2 |\n|----|----|\n| a  | b  |";
        var markdigTable = ParseTable(md);
        var builder = new TableBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build(markdigTable, _theme, DefaultAvailableWidth);

        var rows = table.Elements<WTableRow>().ToList();
        // Non-header rows should have CantSplit = false (allow page split)
        for (int i = 1; i < rows.Count; i++)
        {
            var rowProps = rows[i].GetFirstChild<TableRowProperties>();
            rowProps.ShouldNotBeNull();
            var cantSplit = rowProps!.GetFirstChild<CantSplit>();
            if (cantSplit != null)
            {
                cantSplit.Val!.Value.ShouldBe(OnOffOnlyValues.Off);
            }
        }
    }
}
