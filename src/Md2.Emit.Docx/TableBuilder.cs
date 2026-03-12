// agent-notes: { ctx: "Builds OpenXml Table with auto-sizing, borders, header repeat, alternating rows, cell padding, inline formatting", deps: [ParagraphBuilder, ResolvedTheme, Markdig.Extensions.Tables, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

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

/// <summary>
/// Delegate that converts a Markdig inline into OpenXml elements, preserving formatting.
/// Parameters: inline node, bold, italic, strikethrough.
/// </summary>
public delegate IEnumerable<OpenXmlElement> InlineVisitorDelegate(
    Markdig.Syntax.Inlines.Inline inline, bool bold, bool italic, bool strikethrough);

public sealed class TableBuilder
{
    private readonly ParagraphBuilder _paragraphBuilder;
    private readonly InlineVisitorDelegate? _inlineVisitor;

    private const double MinColumnFraction = 0.05;
    private const double MaxColumnFraction = 0.60;

    public TableBuilder(ParagraphBuilder paragraphBuilder)
        : this(paragraphBuilder, null)
    {
    }

    public TableBuilder(ParagraphBuilder paragraphBuilder, InlineVisitorDelegate? inlineVisitor)
    {
        _paragraphBuilder = paragraphBuilder;
        _inlineVisitor = inlineVisitor;
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
                    var elements = BuildElementsFromInline(inline, isHeader, theme);
                    foreach (var element in elements)
                    {
                        paragraph.Append(element);
                    }
                }
            }
        }

        return paragraph;
    }

    private IEnumerable<OpenXmlElement> BuildElementsFromInline(
        Markdig.Syntax.Inlines.Inline inline, bool isHeader, ResolvedTheme theme)
    {
        if (_inlineVisitor != null)
        {
            // Use the full inline visitor chain which preserves bold, italic,
            // strikethrough, links, and inline code formatting.
            // Note: bold=false here; header bold is applied uniformly by ApplyHeaderOverrides
            // to avoid double-bolding when cells also contain **bold** markdown.
            var elements = _inlineVisitor(inline, false, false, false)
                .Where(e => e is not Paragraph) // Guard: images return Paragraphs which can't nest in cells
                .ToList();

            if (isHeader)
            {
                ApplyHeaderOverrides(elements, theme);
            }

            return elements;
        }

        // Fallback: plain-text extraction (for backward compatibility with tests
        // that construct TableBuilder without an inline visitor).
        return BuildRunFromInlineFallback(inline, isHeader, theme);
    }

    /// <summary>
    /// Applies header styling overrides (bold + white color) to all runs in the element tree.
    /// Hyperlink runs also get the white color override so they remain visible on the dark header background.
    /// </summary>
    private static void ApplyHeaderOverrides(IEnumerable<OpenXmlElement> elements, ResolvedTheme theme)
    {
        foreach (var element in elements)
        {
            foreach (var run in element is Run r ? new[] { r } : element.Descendants<Run>())
            {
                var runProps = run.RunProperties;
                if (runProps == null)
                {
                    runProps = new RunProperties();
                    run.PrependChild(runProps);
                }

                // Force bold on all header text
                if (runProps.Bold == null)
                    runProps.Append(new Bold());

                // Force white color on all header text
                var color = runProps.Color;
                if (color != null)
                    color.Val = theme.TableHeaderForeground;
                else
                    runProps.Append(new Color { Val = theme.TableHeaderForeground });
            }
        }
    }

    private IEnumerable<OpenXmlElement> BuildRunFromInlineFallback(
        Markdig.Syntax.Inlines.Inline inline, bool isHeader, ResolvedTheme theme)
    {
        var text = ExtractInlineText(inline);
        if (string.IsNullOrEmpty(text))
            return Enumerable.Empty<OpenXmlElement>();

        var run = _paragraphBuilder.CreateRun(text, bold: isHeader);

        if (isHeader)
        {
            var runProps = run.RunProperties;
            if (runProps != null)
            {
                var color = runProps.Color;
                if (color != null)
                    color.Val = theme.TableHeaderForeground;
            }
        }

        return new OpenXmlElement[] { run };
    }

    private static string ExtractInlineText(Markdig.Syntax.Inlines.Inline inline) =>
        InlineTextExtractor.Extract(inline);

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
