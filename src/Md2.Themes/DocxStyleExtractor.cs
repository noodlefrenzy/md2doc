// agent-notes: { ctx: "extracts theme properties from DOCX template styles", deps: [ThemeDefinition.cs, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-12" }

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Md2.Themes;

/// <summary>
/// Extracts style properties from a DOCX template into a ThemeDefinition.
/// Best-effort extraction: only properties that are explicitly set in the
/// template's styles.xml and section properties are returned.
/// </summary>
public static class DocxStyleExtractor
{
    private static readonly Dictionary<string, Action<ThemeDefinition, double>> HeadingSizeSetters = new()
    {
        ["Heading1"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading1Size = v; },
        ["Heading2"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading2Size = v; },
        ["Heading3"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading3Size = v; },
        ["Heading4"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading4Size = v; },
        ["Heading5"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading5Size = v; },
        ["Heading6"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading6Size = v; },
    };

    /// <summary>
    /// Extracts theme properties from a DOCX file's styles and page layout.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when the DOCX file does not exist.</exception>
    public static ThemeDefinition Extract(string docxPath)
    {
        if (!File.Exists(docxPath))
            throw new FileNotFoundException("DOCX template not found.", docxPath);

        using var doc = WordprocessingDocument.Open(docxPath, false);
        var theme = new ThemeDefinition();

        var mainPart = doc.MainDocumentPart;
        if (mainPart is null)
            return theme;

        ExtractStyles(mainPart, theme);
        ExtractPageLayout(mainPart, theme);

        return theme;
    }

    private static void ExtractStyles(MainDocumentPart mainPart, ThemeDefinition theme)
    {
        var stylesPart = mainPart.StyleDefinitionsPart;
        if (stylesPart?.Styles is null)
            return;

        foreach (var style in stylesPart.Styles.Elements<Style>())
        {
            var styleId = style.StyleId?.Value;
            if (styleId is null)
                continue;

            var runProps = style.GetFirstChild<StyleRunProperties>();
            if (runProps is null)
                continue;

            if (styleId == "Normal")
            {
                ExtractNormalStyle(runProps, theme);
            }
            else if (styleId == "Hyperlink")
            {
                ExtractHyperlinkStyle(runProps, theme);
            }
            else if (HeadingSizeSetters.ContainsKey(styleId))
            {
                ExtractHeadingStyle(styleId, runProps, theme);
            }
        }
    }

    private static void ExtractNormalStyle(StyleRunProperties runProps, ThemeDefinition theme)
    {
        var font = runProps.GetFirstChild<RunFonts>()?.Ascii?.Value;
        if (font is not null)
        {
            theme.Typography ??= new();
            theme.Typography.BodyFont = font;
        }

        var fontSize = ParseFontSizePoints(runProps);
        if (fontSize.HasValue)
        {
            theme.Docx ??= new();
            theme.Docx.BaseFontSize = fontSize.Value;
        }

        var color = runProps.GetFirstChild<Color>()?.Val?.Value;
        if (color is not null)
        {
            theme.Colors ??= new();
            theme.Colors.BodyText = color;
        }
    }

    private static void ExtractHeadingStyle(string styleId, StyleRunProperties runProps, ThemeDefinition theme)
    {
        var font = runProps.GetFirstChild<RunFonts>()?.Ascii?.Value;
        if (font is not null)
        {
            theme.Typography ??= new();
            // Use first heading font found as the heading font
            theme.Typography.HeadingFont ??= font;
        }

        var fontSize = ParseFontSizePoints(runProps);
        if (fontSize.HasValue && HeadingSizeSetters.TryGetValue(styleId, out var setter))
        {
            setter(theme, fontSize.Value);
        }
    }

    private static void ExtractHyperlinkStyle(StyleRunProperties runProps, ThemeDefinition theme)
    {
        var color = runProps.GetFirstChild<Color>()?.Val?.Value;
        if (color is not null)
        {
            theme.Colors ??= new();
            theme.Colors.Link = color;
        }
    }

    private static void ExtractPageLayout(MainDocumentPart mainPart, ThemeDefinition theme)
    {
        var body = mainPart.Document?.Body;
        if (body is null)
            return;

        var sectPr = body.GetFirstChild<SectionProperties>();
        if (sectPr is null)
            return;

        var pageSize = sectPr.GetFirstChild<PageSize>();
        if (pageSize is not null)
        {
            theme.Docx ??= new();
            theme.Docx.Page ??= new();

            if (pageSize.Width?.HasValue == true)
                theme.Docx.Page.Width = pageSize.Width.Value;
            if (pageSize.Height?.HasValue == true)
                theme.Docx.Page.Height = pageSize.Height.Value;
        }

        var pageMargin = sectPr.GetFirstChild<PageMargin>();
        if (pageMargin is not null)
        {
            theme.Docx ??= new();
            theme.Docx.Page ??= new();

            if (pageMargin.Top?.HasValue == true)
                theme.Docx.Page.MarginTop = pageMargin.Top.Value;
            if (pageMargin.Bottom?.HasValue == true)
                theme.Docx.Page.MarginBottom = pageMargin.Bottom.Value;
            if (pageMargin.Left?.HasValue == true)
                theme.Docx.Page.MarginLeft = (int)pageMargin.Left.Value;
            if (pageMargin.Right?.HasValue == true)
                theme.Docx.Page.MarginRight = (int)pageMargin.Right.Value;
        }
    }

    /// <summary>
    /// Parses font size from half-points to points.
    /// </summary>
    private static double? ParseFontSizePoints(StyleRunProperties runProps)
    {
        var fontSizeVal = runProps.GetFirstChild<FontSize>()?.Val?.Value;
        if (fontSizeVal is not null && double.TryParse(fontSizeVal, out var halfPoints))
        {
            return halfPoints / 2.0;
        }
        return null;
    }
}
