// agent-notes: { ctx: "Builds OpenXml Table for fenced code blocks with mono font, background shading", deps: [ParagraphBuilder, ResolvedTheme, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-12" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Pipeline;

namespace Md2.Emit.Docx;

public sealed class CodeBlockBuilder
{
    private readonly ParagraphBuilder _paragraphBuilder;

    public CodeBlockBuilder(ParagraphBuilder paragraphBuilder)
    {
        _paragraphBuilder = paragraphBuilder;
    }

    /// <summary>
    /// Builds a code block as a single-cell table with mono font and background shading.
    /// </summary>
    /// <param name="code">The code text content.</param>
    /// <param name="language">Optional language identifier (for future syntax highlighting).</param>
    /// <param name="theme">The resolved theme for styling.</param>
    public Table Build(string code, string? language, ResolvedTheme theme)
    {
        var codeFontSize = Math.Max(theme.BaseFontSize - 1, 8);
        var halfPoints = ((int)(codeFontSize * 2)).ToString();

        var table = new Table();

        // Table properties: full width, borders, no cell spacing
        var tableProps = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = theme.CodeBlockBorderColor },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = theme.CodeBlockBorderColor },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = theme.CodeBlockBorderColor },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = theme.CodeBlockBorderColor }
            ),
            new TableCellMarginDefault(
                new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 120, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 120, Type = TableWidthValues.Dxa }
            )
        );
        table.Append(tableProps);

        var row = new TableRow();
        var cell = new TableCell();

        // Cell properties: background shading
        var cellProps = new TableCellProperties(
            new Shading
            {
                Val = ShadingPatternValues.Clear,
                Fill = theme.CodeBackgroundColor,
                Color = "auto"
            }
        );
        cell.Append(cellProps);

        // Split code into lines, one paragraph per line
        var lines = code.Split('\n');

        // Remove trailing empty line (common in fenced code blocks)
        if (lines.Length > 0 && string.IsNullOrEmpty(lines[^1]))
        {
            lines = lines[..^1];
        }

        if (lines.Length == 0)
        {
            // Ensure cell has at least one paragraph (OpenXml requirement)
            cell.Append(CreateCodeParagraph("", halfPoints, theme));
        }
        else
        {
            foreach (var line in lines)
            {
                cell.Append(CreateCodeParagraph(line, halfPoints, theme));
            }
        }

        row.Append(cell);
        table.Append(row);

        return table;
    }

    private Paragraph CreateCodeParagraph(string text, string halfPoints, ResolvedTheme theme)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(
                new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }
            )
        );

        var run = new Run(
            new RunProperties(
                new RunFonts
                {
                    Ascii = theme.MonoFont,
                    HighAnsi = theme.MonoFont,
                    ComplexScript = theme.MonoFontFallback,
                    EastAsia = theme.MonoFontFallback
                },
                new FontSize { Val = halfPoints },
                new FontSizeComplexScript { Val = halfPoints },
                new Color { Val = theme.BodyTextColor }
            ),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve }
        );

        paragraph.Append(run);
        return paragraph;
    }
}
