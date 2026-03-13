// agent-notes: { ctx: "Builds OpenXml Table for fenced code blocks with contrast handling", deps: [ParagraphBuilder, ResolvedTheme, Md2.Core.Ast.SyntaxToken, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-13" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Ast;
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
    /// If syntax tokens are provided, renders with syntax highlighting colors.
    /// </summary>
    public Table Build(string code, string? language, ResolvedTheme theme,
        IReadOnlyList<SyntaxToken>? syntaxTokens = null)
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

        if (syntaxTokens != null && syntaxTokens.Count > 0)
        {
            BuildHighlightedContent(cell, syntaxTokens, halfPoints, theme);
        }
        else
        {
            BuildPlainContent(cell, code, halfPoints, theme);
        }

        row.Append(cell);
        table.Append(row);

        return table;
    }

    private void BuildHighlightedContent(TableCell cell, IReadOnlyList<SyntaxToken> tokens,
        string halfPoints, ResolvedTheme theme)
    {
        var paragraph = CreateCodeParagraphShell();

        foreach (var token in tokens)
        {
            if (token.Text == "\n")
            {
                // End current paragraph, start new one
                if (!paragraph.HasChildren || paragraph.Elements<Run>().Any() == false)
                {
                    // Empty line — add empty run to prevent collapse
                    paragraph.Append(CreateCodeRun("", halfPoints, theme.BodyTextColor, SyntaxFontStyle.Normal, theme));
                }
                cell.Append(paragraph);
                paragraph = CreateCodeParagraphShell();
                continue;
            }

            var color = EnsureContrast(token.ForegroundColor ?? theme.BodyTextColor, theme.CodeBackgroundColor);
            paragraph.Append(CreateCodeRun(token.Text, halfPoints, color, token.FontStyle, theme));
        }

        // Append last paragraph
        if (!paragraph.HasChildren || paragraph.Elements<Run>().Any() == false)
        {
            paragraph.Append(CreateCodeRun("", halfPoints, theme.BodyTextColor, SyntaxFontStyle.Normal, theme));
        }
        cell.Append(paragraph);
    }

    private void BuildPlainContent(TableCell cell, string code, string halfPoints, ResolvedTheme theme)
    {
        var lines = code.Split('\n');

        // Remove trailing empty line (common in fenced code blocks)
        if (lines.Length > 0 && string.IsNullOrEmpty(lines[^1]))
        {
            lines = lines[..^1];
        }

        if (lines.Length == 0)
        {
            cell.Append(CreateCodeParagraph("", halfPoints, theme));
        }
        else
        {
            foreach (var line in lines)
            {
                cell.Append(CreateCodeParagraph(line, halfPoints, theme));
            }
        }
    }

    private static Paragraph CreateCodeParagraphShell()
    {
        return new Paragraph(
            new ParagraphProperties(
                new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }
            )
        );
    }

    private Paragraph CreateCodeParagraph(string text, string halfPoints, ResolvedTheme theme)
    {
        var paragraph = CreateCodeParagraphShell();
        paragraph.Append(CreateCodeRun(text, halfPoints, theme.BodyTextColor, SyntaxFontStyle.Normal, theme));
        return paragraph;
    }

    /// <summary>
    /// Returns the foreground color adjusted for contrast against the background.
    /// If the contrast ratio is too low, returns the theme's body text color or its inverse.
    /// </summary>
    internal static string EnsureContrast(string fgHex, string bgHex)
    {
        var fgLum = RelativeLuminance(fgHex);
        var bgLum = RelativeLuminance(bgHex);

        // WCAG contrast ratio: (L1 + 0.05) / (L2 + 0.05) where L1 >= L2
        var lighter = System.Math.Max(fgLum, bgLum);
        var darker = System.Math.Min(fgLum, bgLum);
        var ratio = (lighter + 0.05) / (darker + 0.05);

        // Minimum 3:1 for large text (code blocks use reduced font size)
        if (ratio >= 3.0)
            return fgHex;

        // Insufficient contrast: flip to light or dark based on background
        return bgLum < 0.5 ? "C9D1D9" : "1F2328";
    }

    private static double RelativeLuminance(string hex)
    {
        var r = Convert.ToInt32(hex[..2], 16) / 255.0;
        var g = Convert.ToInt32(hex[2..4], 16) / 255.0;
        var b = Convert.ToInt32(hex[4..6], 16) / 255.0;

        r = r <= 0.03928 ? r / 12.92 : System.Math.Pow((r + 0.055) / 1.055, 2.4);
        g = g <= 0.03928 ? g / 12.92 : System.Math.Pow((g + 0.055) / 1.055, 2.4);
        b = b <= 0.03928 ? b / 12.92 : System.Math.Pow((b + 0.055) / 1.055, 2.4);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private Run CreateCodeRun(string text, string halfPoints, string color,
        SyntaxFontStyle fontStyle, ResolvedTheme theme)
    {
        var runProps = new RunProperties(
            new RunFonts
            {
                Ascii = theme.MonoFont,
                HighAnsi = theme.MonoFont,
                ComplexScript = theme.MonoFontFallback,
                EastAsia = theme.MonoFontFallback
            },
            new FontSize { Val = halfPoints },
            new FontSizeComplexScript { Val = halfPoints },
            new Color { Val = color }
        );

        if (fontStyle.HasFlag(SyntaxFontStyle.Bold))
            runProps.Append(new Bold());
        if (fontStyle.HasFlag(SyntaxFontStyle.Italic))
            runProps.Append(new Italic());

        return new Run(
            runProps,
            new Text(text) { Space = SpaceProcessingModeValues.Preserve }
        );
    }
}
