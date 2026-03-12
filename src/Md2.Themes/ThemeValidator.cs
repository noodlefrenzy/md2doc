// agent-notes: { ctx: "schema validation + unusual value warnings for ThemeDefinition", deps: [ThemeDefinition.cs], state: active, last: "sato@2026-03-12" }

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
