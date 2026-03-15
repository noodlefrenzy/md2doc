// agent-notes: { ctx: "schema validation + unusual value warnings for ThemeDefinition + PPTX section", deps: [ThemeDefinition.cs], state: active, last: "sato@2026-03-15" }

using System.Text.RegularExpressions;

namespace Md2.Themes;

/// <summary>
/// Severity level for theme validation issues.
/// </summary>
public enum ValidationSeverity
{
    Error,
    Warning
}

/// <summary>
/// A single validation issue found in a ThemeDefinition.
/// </summary>
public record ThemeValidationIssue(string Message, ValidationSeverity Severity, string? PropertyPath = null);

/// <summary>
/// Validates ThemeDefinition instances for schema errors and unusual values.
/// Schema violations (invalid hex, non-positive sizes) are Errors.
/// Unusual-but-valid values (extreme font sizes, tight margins) are Warnings.
/// </summary>
public static partial class ThemeValidator
{
    private const double MinReasonableFontSize = 6;
    private const double MaxReasonableFontSize = 72;
    private const double MinReasonableLineSpacing = 0.5;
    private const double MaxReasonableLineSpacing = 3.0;
    private const int MinContentWidthTwips = 2880; // 2 inches

    [GeneratedRegex("^[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();

    /// <summary>
    /// Validates a ThemeDefinition and returns all issues found.
    /// </summary>
    public static IReadOnlyList<ThemeValidationIssue> Validate(ThemeDefinition theme)
    {
        var issues = new List<ThemeValidationIssue>();

        ValidateColors(theme.Colors, issues);
        ValidateDocx(theme.Docx, issues);
        ValidatePptx(theme.Pptx, issues);

        return issues;
    }

    private static void ValidateColors(ThemeColorsSection? colors, List<ThemeValidationIssue> issues)
    {
        if (colors is null) return;

        ValidateHexColor(colors.Primary, "Colors.Primary", issues);
        ValidateHexColor(colors.Secondary, "Colors.Secondary", issues);
        ValidateHexColor(colors.BodyText, "Colors.BodyText", issues);
        ValidateHexColor(colors.CodeBackground, "Colors.CodeBackground", issues);
        ValidateHexColor(colors.CodeBorder, "Colors.CodeBorder", issues);
        ValidateHexColor(colors.Link, "Colors.Link", issues);
        ValidateHexColor(colors.TableHeaderBackground, "Colors.TableHeaderBackground", issues);
        ValidateHexColor(colors.TableHeaderForeground, "Colors.TableHeaderForeground", issues);
        ValidateHexColor(colors.TableBorder, "Colors.TableBorder", issues);
        ValidateHexColor(colors.TableAlternateRow, "Colors.TableAlternateRow", issues);
        ValidateHexColor(colors.BlockquoteBorder, "Colors.BlockquoteBorder", issues);
        ValidateHexColor(colors.BlockquoteText, "Colors.BlockquoteText", issues);
    }

    private static void ValidateHexColor(string? value, string propertyPath, List<ThemeValidationIssue> issues)
    {
        if (value is null) return;

        if (!HexColorRegex().IsMatch(value))
        {
            issues.Add(new ThemeValidationIssue(
                $"Invalid hex color '{value}' — expected 6-character hex string (e.g. 'FF0000').",
                ValidationSeverity.Error,
                propertyPath));
        }
    }

    private static void ValidateDocx(ThemeDocxSection? docx, List<ThemeValidationIssue> issues)
    {
        if (docx is null) return;

        ValidatePositiveDouble(docx.BaseFontSize, "Docx.BaseFontSize", "Font size", issues);
        ValidatePositiveDouble(docx.Heading1Size, "Docx.Heading1Size", "Heading size", issues);
        ValidatePositiveDouble(docx.Heading2Size, "Docx.Heading2Size", "Heading size", issues);
        ValidatePositiveDouble(docx.Heading3Size, "Docx.Heading3Size", "Heading size", issues);
        ValidatePositiveDouble(docx.Heading4Size, "Docx.Heading4Size", "Heading size", issues);
        ValidatePositiveDouble(docx.Heading5Size, "Docx.Heading5Size", "Heading size", issues);
        ValidatePositiveDouble(docx.Heading6Size, "Docx.Heading6Size", "Heading size", issues);
        ValidatePositiveDouble(docx.LineSpacing, "Docx.LineSpacing", "Line spacing", issues);

        ValidateNonNegativeInt(docx.TableBorderWidth, "Docx.TableBorderWidth", "Table border width", issues);
        ValidateNonNegativeInt(docx.BlockquoteIndentTwips, "Docx.BlockquoteIndentTwips", "Blockquote indent", issues);

        ValidatePage(docx.Page, issues);

        // Warnings for unusual-but-valid values (only if no error for that property)
        var errorPaths = new HashSet<string>(issues.Where(i => i.Severity == ValidationSeverity.Error && i.PropertyPath is not null).Select(i => i.PropertyPath!));

        WarnFontSizeRange(docx.BaseFontSize, "Docx.BaseFontSize", errorPaths, issues);
        WarnFontSizeRange(docx.Heading1Size, "Docx.Heading1Size", errorPaths, issues);
        WarnFontSizeRange(docx.Heading2Size, "Docx.Heading2Size", errorPaths, issues);
        WarnFontSizeRange(docx.Heading3Size, "Docx.Heading3Size", errorPaths, issues);
        WarnFontSizeRange(docx.Heading4Size, "Docx.Heading4Size", errorPaths, issues);
        WarnFontSizeRange(docx.Heading5Size, "Docx.Heading5Size", errorPaths, issues);
        WarnFontSizeRange(docx.Heading6Size, "Docx.Heading6Size", errorPaths, issues);

        WarnLineSpacingRange(docx.LineSpacing, "Docx.LineSpacing", errorPaths, issues);
        WarnContentWidth(docx.Page, issues);
    }

    private static void ValidatePage(ThemePageSection? page, List<ThemeValidationIssue> issues)
    {
        if (page is null) return;

        if (page.Width is 0)
            issues.Add(new ThemeValidationIssue("Page width must be positive.", ValidationSeverity.Error, "Docx.Page.Width"));

        if (page.Height is 0)
            issues.Add(new ThemeValidationIssue("Page height must be positive.", ValidationSeverity.Error, "Docx.Page.Height"));

        ValidateNonNegativeInt(page.MarginTop, "Docx.Page.MarginTop", "Page margin top", issues);
        ValidateNonNegativeInt(page.MarginBottom, "Docx.Page.MarginBottom", "Page margin bottom", issues);
        ValidateNonNegativeInt(page.MarginLeft, "Docx.Page.MarginLeft", "Page margin left", issues);
        ValidateNonNegativeInt(page.MarginRight, "Docx.Page.MarginRight", "Page margin right", issues);
    }

    private static void ValidatePositiveDouble(double? value, string propertyPath, string label, List<ThemeValidationIssue> issues)
    {
        if (value is null) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            issues.Add(new ThemeValidationIssue(
                $"{label} must be a finite number, got {value.Value}.",
                ValidationSeverity.Error,
                propertyPath));
        }
        else if (value.Value <= 0)
        {
            issues.Add(new ThemeValidationIssue(
                $"{label} must be positive, got {value.Value}.",
                ValidationSeverity.Error,
                propertyPath));
        }
    }

    private static void ValidateNonNegativeInt(int? value, string propertyPath, string label, List<ThemeValidationIssue> issues)
    {
        if (value is null) return;
        if (value.Value < 0)
        {
            issues.Add(new ThemeValidationIssue(
                $"{label} must be non-negative, got {value.Value}.",
                ValidationSeverity.Error,
                propertyPath));
        }
    }

    private static void WarnFontSizeRange(double? value, string propertyPath, HashSet<string> errorPaths, List<ThemeValidationIssue> issues)
    {
        if (value is null || errorPaths.Contains(propertyPath)) return;
        if (value.Value < MinReasonableFontSize || value.Value > MaxReasonableFontSize)
        {
            issues.Add(new ThemeValidationIssue(
                $"Font size {value.Value}pt is outside the typical range ({MinReasonableFontSize}-{MaxReasonableFontSize}pt).",
                ValidationSeverity.Warning,
                propertyPath));
        }
    }

    private static void WarnLineSpacingRange(double? value, string propertyPath, HashSet<string> errorPaths, List<ThemeValidationIssue> issues)
    {
        if (value is null || errorPaths.Contains(propertyPath)) return;
        if (value.Value < MinReasonableLineSpacing || value.Value > MaxReasonableLineSpacing)
        {
            issues.Add(new ThemeValidationIssue(
                $"Line spacing {value.Value} is outside the typical range ({MinReasonableLineSpacing}-{MaxReasonableLineSpacing}).",
                ValidationSeverity.Warning,
                propertyPath));
        }
    }

    private static void ValidatePptx(ThemePptxSection? pptx, List<ThemeValidationIssue> issues)
    {
        if (pptx is null) return;

        ValidatePositiveDouble(pptx.BaseFontSize, "Pptx.BaseFontSize", "Font size", issues);
        ValidatePositiveDouble(pptx.Heading1Size, "Pptx.Heading1Size", "Heading size", issues);
        ValidatePositiveDouble(pptx.Heading2Size, "Pptx.Heading2Size", "Heading size", issues);
        ValidatePositiveDouble(pptx.Heading3Size, "Pptx.Heading3Size", "Heading size", issues);

        // Validate slide size string
        if (pptx.SlideSize is not null && pptx.SlideSize is not ("16:9" or "4:3" or "16:10"))
        {
            issues.Add(new ThemeValidationIssue(
                $"Invalid slide size '{pptx.SlideSize}' — expected '16:9', '4:3', or '16:10'.",
                ValidationSeverity.Error,
                "Pptx.SlideSize"));
        }

        // Per-format color overrides
        if (pptx.Colors is not null)
            ValidateColors(pptx.Colors, issues, "Pptx.Colors");

        // Background color
        ValidateHexColor(pptx.Background?.Color, "Pptx.Background.Color", issues);

        // Layout sections
        ValidatePositiveDouble(pptx.TitleSlide?.TitleSize, "Pptx.TitleSlide.TitleSize", "Title size", issues);
        ValidatePositiveDouble(pptx.TitleSlide?.SubtitleSize, "Pptx.TitleSlide.SubtitleSize", "Subtitle size", issues);
        ValidateHexColor(pptx.TitleSlide?.BackgroundColor, "Pptx.TitleSlide.BackgroundColor", issues);

        ValidatePositiveDouble(pptx.SectionDivider?.TitleSize, "Pptx.SectionDivider.TitleSize", "Title size", issues);
        ValidateHexColor(pptx.SectionDivider?.BackgroundColor, "Pptx.SectionDivider.BackgroundColor", issues);

        ValidatePositiveDouble(pptx.Content?.TitleSize, "Pptx.Content.TitleSize", "Title size", issues);
        ValidatePositiveDouble(pptx.Content?.BodySize, "Pptx.Content.BodySize", "Body size", issues);
        ValidatePositiveDouble(pptx.Content?.BulletIndent, "Pptx.Content.BulletIndent", "Bullet indent", issues);

        ValidatePositiveDouble(pptx.TwoColumn?.Gutter, "Pptx.TwoColumn.Gutter", "Gutter", issues);

        ValidatePositiveDouble(pptx.CodeBlock?.FontSize, "Pptx.CodeBlock.FontSize", "Code font size", issues);
        ValidatePositiveDouble(pptx.CodeBlock?.Padding, "Pptx.CodeBlock.Padding", "Code padding", issues);

        // Chart palette colors
        if (pptx.ChartPalette is not null)
        {
            for (int i = 0; i < pptx.ChartPalette.Count; i++)
            {
                ValidateHexColor(ThemeColorsSection.NormalizeHex(pptx.ChartPalette[i]),
                    $"Pptx.ChartPalette[{i}]", issues);
            }
        }

        // PPTX font size warnings (different range: slides use larger sizes)
        var errorPaths = new HashSet<string>(
            issues.Where(i => i.Severity == ValidationSeverity.Error && i.PropertyPath is not null)
                  .Select(i => i.PropertyPath!));

        WarnPptxFontSizeRange(pptx.BaseFontSize, "Pptx.BaseFontSize", errorPaths, issues);
        WarnPptxFontSizeRange(pptx.Heading1Size, "Pptx.Heading1Size", errorPaths, issues);
        WarnPptxFontSizeRange(pptx.Heading2Size, "Pptx.Heading2Size", errorPaths, issues);
        WarnPptxFontSizeRange(pptx.Heading3Size, "Pptx.Heading3Size", errorPaths, issues);
    }

    private static void ValidateColors(ThemeColorsSection colors, List<ThemeValidationIssue> issues, string prefix)
    {
        ValidateHexColor(colors.Primary, $"{prefix}.Primary", issues);
        ValidateHexColor(colors.Secondary, $"{prefix}.Secondary", issues);
        ValidateHexColor(colors.BodyText, $"{prefix}.BodyText", issues);
        ValidateHexColor(colors.CodeBackground, $"{prefix}.CodeBackground", issues);
        ValidateHexColor(colors.CodeBorder, $"{prefix}.CodeBorder", issues);
        ValidateHexColor(colors.Link, $"{prefix}.Link", issues);
        ValidateHexColor(colors.TableHeaderBackground, $"{prefix}.TableHeaderBackground", issues);
        ValidateHexColor(colors.TableHeaderForeground, $"{prefix}.TableHeaderForeground", issues);
        ValidateHexColor(colors.TableBorder, $"{prefix}.TableBorder", issues);
        ValidateHexColor(colors.TableAlternateRow, $"{prefix}.TableAlternateRow", issues);
        ValidateHexColor(colors.BlockquoteBorder, $"{prefix}.BlockquoteBorder", issues);
        ValidateHexColor(colors.BlockquoteText, $"{prefix}.BlockquoteText", issues);
    }

    private static void WarnPptxFontSizeRange(double? value, string propertyPath, HashSet<string> errorPaths, List<ThemeValidationIssue> issues)
    {
        if (value is null || errorPaths.Contains(propertyPath)) return;
        // PPTX uses larger font sizes than DOCX: typical range 10-120pt
        if (value.Value < MinReasonableFontSize || value.Value > 120)
        {
            issues.Add(new ThemeValidationIssue(
                $"PPTX font size {value.Value}pt is outside the typical range ({MinReasonableFontSize}-120pt).",
                ValidationSeverity.Warning,
                propertyPath));
        }
    }

    private static void WarnContentWidth(ThemePageSection? page, List<ThemeValidationIssue> issues)
    {
        if (page?.Width is null) return;

        long marginLeft = page.MarginLeft ?? 0;
        long marginRight = page.MarginRight ?? 0;
        long contentWidth = (long)page.Width.Value - marginLeft - marginRight;

        if (contentWidth < MinContentWidthTwips)
        {
            var contentInches = contentWidth / 1440.0;
            issues.Add(new ThemeValidationIssue(
                $"Page margins leave only {contentInches:F1} inches of content width, which may be too narrow.",
                ValidationSeverity.Warning));
        }
    }
}
