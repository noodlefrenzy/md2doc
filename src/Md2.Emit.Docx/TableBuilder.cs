// agent-notes: { ctx: "Builds OpenXml Table with auto-sizing, borders, header repeat, alternating rows, cell padding", deps: [ParagraphBuilder, ResolvedTheme, Markdig.Extensions.Tables, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Core.Pipeline;

using MdTable = Markdig.Extensions.Tables.Table;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;

namespace Md2.Emit.Docx;

public sealed class TableBuilder
{
    private readonly ParagraphBuilder _paragraphBuilder;

    private const double MinColumnFraction = 0.05;
    private const double MaxColumnFraction = 0.60;

    public TableBuilder(ParagraphBuilder paragraphBuilder)
    {
        _paragraphBuilder = paragraphBuilder;
    }

    public Table Build(MdTable markdigTable, ResolvedTheme theme, int availableWidthTwips)
    {
        var rows = markdigTable.OfType<MdTableRow>().ToList();
        // ColumnDefinitions.Count can overcount (includes leading pipe).
        // Use actual cell count from rows as the authoritative column count.
        var columnCount = DetermineColumnCount(rows);

        var columnWidths = CalculateColumnWidths(rows, columnCount, availableWidthTwips);

        var tableGrid = new TableGrid();
        foreach (var width in columnWidths)
        {
            tableGrid.Append(new GridColumn { Width = width.ToString() });
        }

        var table = new Table(
            CreateTableProperties(theme, availableWidthTwips),
            tableGrid
        );

        int dataRowIndex = 0;
        foreach (var mdRow in rows)
        {
            var tableRow = BuildRow(mdRow, columnWidths, theme, mdRow.IsHeader ? -1 : dataRowIndex);
            table.Append(tableRow);
            if (!mdRow.IsHeader)
                dataRowIndex++;
        }

        return table;
    }

    private static TableProperties CreateTableProperties(ResolvedTheme theme, int availableWidthTwips)
    {
        var borderValue = new EnumValue<BorderValues>(BorderValues.Single);
        var borderColor = theme.TableBorderColor;
        var borderSize = (uint)theme.TableBorderWidth;

        return new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableBorders(
                new TopBorder { Val = borderValue, Color = borderColor, Size = borderSize },
                new BottomBorder { Val = borderValue, Color = borderColor, Size = borderSize },
                new LeftBorder { Val = borderValue, Color = borderColor, Size = borderSize },
                new RightBorder { Val = borderValue, Color = borderColor, Size = borderSize },
                new InsideHorizontalBorder { Val = borderValue, Color = borderColor, Size = borderSize },
                new InsideVerticalBorder { Val = borderValue, Color = borderColor, Size = borderSize }
            )
        );
    }

    private TableRow BuildRow(MdTableRow mdRow, int[] columnWidths, ResolvedTheme theme, int dataRowIndex)
    {
        var cells = mdRow.OfType<MdTableCell>().ToList();
        var tableRow = new TableRow();

        // Add row properties: header repeat for header rows, CantSplit=false for data rows
        var rowProps = new TableRowProperties();
        if (mdRow.IsHeader)
        {
            rowProps.Append(new TableHeader());
        }
        else
        {
            rowProps.Append(new CantSplit { Val = OnOffOnlyValues.Off });
        }
        tableRow.Append(rowProps);

        bool isAlternateDataRow = !mdRow.IsHeader && dataRowIndex % 2 == 1;

        for (int i = 0; i < columnWidths.Length; i++)
        {
            var cellContent = i < cells.Count ? cells[i] : null;
            var tableCell = BuildCell(cellContent, columnWidths[i], mdRow.IsHeader, theme, isAlternateDataRow);
            tableRow.Append(tableCell);
        }

        return tableRow;
    }

    private const int DefaultCellPaddingTwips = 57; // ~1mm each side

    private TableCell BuildCell(MdTableCell? mdCell, int widthTwips, bool isHeader, ResolvedTheme theme, bool isAlternateDataRow = false)
    {
        var cellProperties = new TableCellProperties(
            new TableCellWidth { Width = widthTwips.ToString(), Type = TableWidthUnitValues.Dxa }
        );

        if (isHeader)
        {
            cellProperties.Append(new Shading
            {
                Val = ShadingPatternValues.Clear,
                Fill = theme.TableHeaderBackground
            });
        }
        else if (isAlternateDataRow)
        {
            cellProperties.Append(new Shading
            {
                Val = ShadingPatternValues.Clear,
                Fill = theme.TableAlternateRowBackground
            });
        }

        // Cell padding
        var paddingStr = DefaultCellPaddingTwips.ToString();
        cellProperties.Append(new TableCellMargin(
            new TopMargin { Width = paddingStr, Type = TableWidthUnitValues.Dxa },
            new BottomMargin { Width = paddingStr, Type = TableWidthUnitValues.Dxa },
            new LeftMargin { Width = paddingStr, Type = TableWidthUnitValues.Dxa },
            new RightMargin { Width = paddingStr, Type = TableWidthUnitValues.Dxa }
        ));

        var paragraph = BuildCellParagraph(mdCell, isHeader, theme);

        return new TableCell(cellProperties, paragraph);
    }

    private Paragraph BuildCellParagraph(MdTableCell? mdCell, bool isHeader, ResolvedTheme theme)
    {
        var paragraph = _paragraphBuilder.CreateBodyParagraph();

        if (mdCell == null)
            return paragraph;

        // Markdig TableCell contains blocks. Usually a single ParagraphBlock.
        foreach (var block in mdCell)
        {
            if (block is ParagraphBlock paragraphBlock && paragraphBlock.Inline != null)
            {
                foreach (var inline in paragraphBlock.Inline)
                {
                    var run = BuildRunFromInline(inline, isHeader, theme);
                    if (run != null)
                        paragraph.Append(run);
                }
            }
        }

        return paragraph;
    }

    private Run? BuildRunFromInline(Markdig.Syntax.Inlines.Inline inline, bool isHeader, ResolvedTheme theme)
    {
        var text = ExtractInlineText(inline);
        if (string.IsNullOrEmpty(text))
            return null;

        var run = _paragraphBuilder.CreateRun(text, bold: isHeader);

        if (isHeader)
        {
            // Override color for header text
            var runProps = run.RunProperties;
            if (runProps != null)
            {
                var color = runProps.Color;
                if (color != null)
                    color.Val = theme.TableHeaderForeground;
            }
        }

        return run;
    }

    private static string ExtractInlineText(Markdig.Syntax.Inlines.Inline inline)
    {
        return inline switch
        {
            LiteralInline literal => literal.Content.ToString(),
            EmphasisInline emphasis => string.Join("", emphasis.Select(ExtractInlineText)),
            CodeInline code => code.Content,
            ContainerInline container => string.Join("", container.Select(ExtractInlineText)),
            _ => string.Empty
        };
    }

    private static int[] CalculateColumnWidths(List<MdTableRow> rows, int columnCount, int availableWidthTwips)
    {
        // Step 1: Calculate max content length per column
        var maxLengths = new int[columnCount];
        for (int i = 0; i < columnCount; i++)
            maxLengths[i] = 1; // minimum 1 to avoid division by zero

        foreach (var row in rows)
        {
            var cells = row.OfType<MdTableCell>().ToList();
            for (int i = 0; i < columnCount && i < cells.Count; i++)
            {
                var length = GetCellTextLength(cells[i]);
                if (length > maxLengths[i])
                    maxLengths[i] = length;
            }
        }

        // Step 2: Calculate proportional widths
        var totalMaxLen = maxLengths.Sum();
        var rawFractions = new double[columnCount];
        for (int i = 0; i < columnCount; i++)
        {
            rawFractions[i] = (double)maxLengths[i] / totalMaxLen;
        }

        // Step 3: Convert to twip widths with min/max constraints
        var minTwips = (int)(MinColumnFraction * availableWidthTwips);
        var maxTwips = (int)(MaxColumnFraction * availableWidthTwips);

        var widths = new int[columnCount];
        for (int i = 0; i < columnCount; i++)
        {
            widths[i] = Math.Clamp(
                (int)Math.Round(rawFractions[i] * availableWidthTwips),
                minTwips,
                maxTwips);
        }

        // Step 4: Redistribute to sum exactly to availableWidthTwips
        var currentSum = widths.Sum();
        var diff = availableWidthTwips - currentSum;
        // Distribute remainder across columns that aren't at max
        while (diff != 0)
        {
            var step = diff > 0 ? 1 : -1;
            bool changed = false;
            for (int i = 0; i < columnCount && diff != 0; i++)
            {
                if (step > 0 && widths[i] < maxTwips)
                {
                    widths[i] += step;
                    diff -= step;
                    changed = true;
                }
                else if (step < 0 && widths[i] > minTwips)
                {
                    widths[i] += step;
                    diff -= step;
                    changed = true;
                }
            }
            if (!changed) break; // all columns at limits
        }

        return widths;
    }

    private static int GetCellTextLength(MdTableCell cell)
    {
        var length = 0;
        foreach (var block in cell)
        {
            if (block is ParagraphBlock paragraphBlock && paragraphBlock.Inline != null)
            {
                foreach (var inline in paragraphBlock.Inline)
                {
                    length += ExtractInlineText(inline).Length;
                }
            }
        }
        return Math.Max(length, 1);
    }

    private static int DetermineColumnCount(List<MdTableRow> rows)
    {
        if (rows.Count == 0)
            return 0;

        return rows.Max(r => r.OfType<MdTableCell>().Count());
    }
}
