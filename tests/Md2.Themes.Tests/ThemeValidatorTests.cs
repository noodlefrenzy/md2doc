// agent-notes: { ctx: "TDD tests for ThemeValidator schema+warning checks", deps: [src/Md2.Themes/ThemeValidator.cs, src/Md2.Themes/ThemeDefinition.cs], state: active, last: "sato@2026-03-12" }

using Shouldly;
using Md2.Themes;

namespace Md2.Themes.Tests;

public class ThemeValidatorTests
{
    // ─── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a fully valid ThemeDefinition with reasonable defaults.
    /// Tests that need invalid values override specific properties.
    /// </summary>
    private static ThemeDefinition MakeValidTheme() => new()
    {
        Meta = new ThemeMetaSection
        {
            Name = "test-theme",
            Description = "A valid test theme",
            Version = 1,
        },
        Typography = new ThemeTypographySection
        {
            HeadingFont = "Arial",
            BodyFont = "Georgia",
            MonoFont = "Fira Code",
        },
        Colors = new ThemeColorsSection
        {
            Primary = "1B3A5C",
            Secondary = "4A90D9",
            BodyText = "333333",
            CodeBackground = "F5F5F5",
            CodeBorder = "E0E0E0",
            Link = "4A90D9",
            TableHeaderBackground = "1B3A5C",
            TableHeaderForeground = "FFFFFF",
            TableBorder = "BFBFBF",
            TableAlternateRow = "F2F2F2",
            BlockquoteBorder = "4A90D9",
            BlockquoteText = "555555",
        },
        Docx = new ThemeDocxSection
        {
            BaseFontSize = 12,
            Heading1Size = 30,
            Heading2Size = 24,
            Heading3Size = 18,
            Heading4Size = 14,
            Heading5Size = 12,
            Heading6Size = 12,
            LineSpacing = 1.15,
            TableBorderWidth = 6,
            BlockquoteIndentTwips = 720,
            Page = new ThemePageSection
            {
                Width = 12240,   // 8.5 inches in twips
                Height = 15840,  // 11 inches in twips
                MarginTop = 1440,
                MarginBottom = 1440,
                MarginLeft = 1800,
                MarginRight = 1800,
            },
        },
    };

    // ─── AC-6.2.2: Valid theme returns empty issues list ───────────────

    [Fact]
    public void Validate_ValidTheme_ReturnsEmptyList()
    {
        var theme = MakeValidTheme();

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_EmptyTheme_AllSectionsNull_ReturnsEmptyList()
    {
        // A theme with all null sections is valid (partial theme for cascading)
        var theme = new ThemeDefinition();

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_PartialTheme_OnlyMeta_ReturnsEmptyList()
    {
        var theme = new ThemeDefinition
        {
            Meta = new ThemeMetaSection { Name = "partial", Version = 1 },
        };

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldBeEmpty();
    }

    // ─── AC-6.2.1: Schema errors — invalid hex colors ─────────────────

    [Theory]
    [InlineData("GG0000")]    // invalid hex chars
    [InlineData("12345")]     // too short (5 chars)
    [InlineData("1234567")]   // too long (7 chars)
    [InlineData("XYZ")]       // 3-char shorthand not supported
    [InlineData("")]          // empty string
    public void Validate_InvalidHexColor_ReturnsError(string badColor)
    {
        var theme = MakeValidTheme();
        theme.Colors!.Primary = badColor;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("Primary", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MultipleInvalidColors_ReturnsErrorForEach()
    {
        var theme = MakeValidTheme();
        theme.Colors!.Primary = "ZZZZZZ";
        theme.Colors.Secondary = "YYYYYY";
        theme.Colors.Link = "nothex";

        var issues = ThemeValidator.Validate(theme);

        var colorErrors = issues.Where(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null).ToList();

        colorErrors.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Validate_ValidHexColors_NoErrors()
    {
        var theme = MakeValidTheme();
        // All colors set by MakeValidTheme() are valid 6-char hex
        var issues = ThemeValidator.Validate(theme);
        issues.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NullColorValue_IsAllowed()
    {
        // Null means "not specified" — perfectly valid for partial themes
        var theme = MakeValidTheme();
        theme.Colors!.Primary = null;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldNotContain(i =>
            i.PropertyPath != null &&
            i.PropertyPath.Contains("Primary", StringComparison.OrdinalIgnoreCase));
    }

    // ─── AC-6.2.1: Schema errors — font sizes must be positive ────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public void Validate_BaseFontSizeNotPositive_ReturnsError(double badSize)
    {
        var theme = MakeValidTheme();
        theme.Docx!.BaseFontSize = badSize;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("BaseFontSize", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_HeadingSizeNotPositive_ReturnsError(double badSize)
    {
        var theme = MakeValidTheme();
        theme.Docx!.Heading1Size = badSize;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("Heading1Size", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_AllHeadingSizesZero_ReturnsErrorForEach()
    {
        var theme = MakeValidTheme();
        theme.Docx!.Heading1Size = 0;
        theme.Docx.Heading2Size = 0;
        theme.Docx.Heading3Size = 0;
        theme.Docx.Heading4Size = 0;
        theme.Docx.Heading5Size = 0;
        theme.Docx.Heading6Size = 0;

        var issues = ThemeValidator.Validate(theme);

        var headingErrors = issues.Where(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("Heading", StringComparison.OrdinalIgnoreCase)).ToList();

        headingErrors.Count.ShouldBe(6);
    }

    [Fact]
    public void Validate_NullFontSize_IsAllowed()
    {
        var theme = MakeValidTheme();
        theme.Docx!.BaseFontSize = null;
        theme.Docx.Heading1Size = null;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldNotContain(i =>
            i.PropertyPath != null &&
            i.PropertyPath.Contains("FontSize", StringComparison.OrdinalIgnoreCase));
        issues.ShouldNotContain(i =>
            i.PropertyPath != null &&
            i.PropertyPath.Contains("Heading1Size", StringComparison.OrdinalIgnoreCase));
    }

    // ─── AC-6.2.1: Schema errors — line spacing must be positive ──────

    [Theory]
    [InlineData(0)]
    [InlineData(-1.0)]
    public void Validate_LineSpacingNotPositive_ReturnsError(double badSpacing)
    {
        var theme = MakeValidTheme();
        theme.Docx!.LineSpacing = badSpacing;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("LineSpacing", StringComparison.OrdinalIgnoreCase));
    }

    // ─── AC-6.2.1: Schema errors — page dimensions must be positive ───

    [Fact]
    public void Validate_PageWidthZero_ReturnsError()
    {
        var theme = MakeValidTheme();
        theme.Docx!.Page!.Width = 0;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("Width", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PageHeightZero_ReturnsError()
    {
        var theme = MakeValidTheme();
        theme.Docx!.Page!.Height = 0;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("Height", StringComparison.OrdinalIgnoreCase));
    }

    // ─── AC-6.2.1: Schema errors — table border width non-negative ────

    [Fact]
    public void Validate_TableBorderWidthNegative_ReturnsError()
    {
        var theme = MakeValidTheme();
        theme.Docx!.TableBorderWidth = -1;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("TableBorderWidth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_TableBorderWidthZero_IsAllowed()
    {
        var theme = MakeValidTheme();
        theme.Docx!.TableBorderWidth = 0;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldNotContain(i =>
            i.PropertyPath != null &&
            i.PropertyPath.Contains("TableBorderWidth", StringComparison.OrdinalIgnoreCase));
    }

    // ─── AC-6.2.1: Schema errors — blockquote indent non-negative ─────

    [Fact]
    public void Validate_BlockquoteIndentNegative_ReturnsError()
    {
        var theme = MakeValidTheme();
        theme.Docx!.BlockquoteIndentTwips = -1;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("BlockquoteIndentTwips", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_BlockquoteIndentZero_IsAllowed()
    {
        var theme = MakeValidTheme();
        theme.Docx!.BlockquoteIndentTwips = 0;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldNotContain(i =>
            i.PropertyPath != null &&
            i.PropertyPath.Contains("BlockquoteIndentTwips", StringComparison.OrdinalIgnoreCase));
    }

    // ─── AC-6.2.3: Unusual value warnings — font sizes ────────────────

    [Theory]
    [InlineData(5)]    // below 6
    [InlineData(4)]
    [InlineData(1)]
    public void Validate_BaseFontSizeTooSmall_ReturnsWarning(double tinySize)
    {
        var theme = MakeValidTheme();
        theme.Docx!.BaseFontSize = tinySize;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("BaseFontSize", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(73)]   // above 72
    [InlineData(100)]
    [InlineData(200)]
    public void Validate_BaseFontSizeTooLarge_ReturnsWarning(double hugeSize)
    {
        var theme = MakeValidTheme();
        theme.Docx!.BaseFontSize = hugeSize;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("BaseFontSize", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(6)]    // boundary — should NOT warn
    [InlineData(12)]   // typical
    [InlineData(72)]   // boundary — should NOT warn
    public void Validate_BaseFontSizeInReasonableRange_NoWarning(double normalSize)
    {
        var theme = MakeValidTheme();
        theme.Docx!.BaseFontSize = normalSize;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldNotContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("BaseFontSize", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_HeadingSizeOutsideRange_ReturnsWarning()
    {
        var theme = MakeValidTheme();
        theme.Docx!.Heading1Size = 100; // above 72

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("Heading1Size", StringComparison.OrdinalIgnoreCase));
    }

    // ─── AC-6.2.3: Unusual value warnings — margins ───────────────────

    [Fact]
    public void Validate_MarginsLeaveLessThanTwoInchesWidth_ReturnsWarning()
    {
        // 2 inches = 2880 twips. Width 12240 - margins must leave >= 2880.
        // MarginLeft + MarginRight >= 12240 - 2880 = 9360
        var theme = MakeValidTheme();
        theme.Docx!.Page!.Width = 12240;
        theme.Docx.Page.MarginLeft = 5000;
        theme.Docx.Page.MarginRight = 5000;
        // Content width = 12240 - 10000 = 2240 twips < 2880 twips (2 inches)

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.Message.Contains("content width", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MarginsLeaveExactlyTwoInchesWidth_NoWarning()
    {
        // 2 inches = 2880 twips
        var theme = MakeValidTheme();
        theme.Docx!.Page!.Width = 12240;
        // Leave exactly 2880 twips of content: 12240 - 2880 = 9360 total margin
        theme.Docx.Page.MarginLeft = 4680;
        theme.Docx.Page.MarginRight = 4680;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldNotContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.Message.Contains("content width", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MarginsCheckSkippedWhenPageWidthNull()
    {
        var theme = MakeValidTheme();
        theme.Docx!.Page!.Width = null;
        theme.Docx.Page.MarginLeft = 5000;
        theme.Docx.Page.MarginRight = 5000;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldNotContain(i =>
            i.Message.Contains("content width", StringComparison.OrdinalIgnoreCase));
    }

    // ─── AC-6.2.3: Unusual value warnings — line spacing ──────────────

    [Theory]
    [InlineData(0.4)]   // below 0.5
    [InlineData(0.1)]
    public void Validate_LineSpacingBelowHalf_ReturnsWarning(double tightSpacing)
    {
        var theme = MakeValidTheme();
        theme.Docx!.LineSpacing = tightSpacing;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("LineSpacing", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(3.1)]   // above 3.0
    [InlineData(5.0)]
    public void Validate_LineSpacingAboveThree_ReturnsWarning(double looseSpacing)
    {
        var theme = MakeValidTheme();
        theme.Docx!.LineSpacing = looseSpacing;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("LineSpacing", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0.5)]   // boundary — should NOT warn
    [InlineData(1.15)]  // typical
    [InlineData(2.0)]   // double spacing
    [InlineData(3.0)]   // boundary — should NOT warn
    public void Validate_LineSpacingInReasonableRange_NoWarning(double normalSpacing)
    {
        var theme = MakeValidTheme();
        theme.Docx!.LineSpacing = normalSpacing;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldNotContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("LineSpacing", StringComparison.OrdinalIgnoreCase));
    }

    // ─── Return type shape ────────────────────────────────────────────

    [Fact]
    public void Validate_ReturnsReadOnlyList()
    {
        var theme = MakeValidTheme();

        IReadOnlyList<ThemeValidationIssue> issues = ThemeValidator.Validate(theme);

        issues.ShouldNotBeNull();
    }

    [Fact]
    public void ThemeValidationIssue_HasExpectedProperties()
    {
        var issue = new ThemeValidationIssue("test message", ValidationSeverity.Error, "some.path");

        issue.Message.ShouldBe("test message");
        issue.Severity.ShouldBe(ValidationSeverity.Error);
        issue.PropertyPath.ShouldBe("some.path");
    }

    [Fact]
    public void ThemeValidationIssue_PropertyPathIsOptional()
    {
        var issue = new ThemeValidationIssue("test message", ValidationSeverity.Warning);

        issue.PropertyPath.ShouldBeNull();
    }

    [Fact]
    public void ValidationSeverity_HasErrorAndWarning()
    {
        ValidationSeverity.Error.ShouldNotBe(ValidationSeverity.Warning);
    }

    // ─── Error vs Warning distinction ─────────────────────────────────

    [Fact]
    public void Validate_SchemaViolations_AreErrors_NotWarnings()
    {
        var theme = MakeValidTheme();
        theme.Docx!.BaseFontSize = -1;   // schema error
        theme.Colors!.Primary = "ZZZZZZ"; // schema error

        var issues = ThemeValidator.Validate(theme);

        var errors = issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
        errors.Count.ShouldBeGreaterThanOrEqualTo(2);

        // These should NOT be warnings
        issues.ShouldNotContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("BaseFontSize", StringComparison.OrdinalIgnoreCase) &&
            i.Message.Contains("positive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_UnusualValues_AreWarnings_NotErrors()
    {
        var theme = MakeValidTheme();
        theme.Docx!.BaseFontSize = 100;  // unusual but valid
        theme.Docx.LineSpacing = 4.0;    // unusual but valid

        var issues = ThemeValidator.Validate(theme);

        var warnings = issues.Where(i => i.Severity == ValidationSeverity.Warning).ToList();
        warnings.Count.ShouldBeGreaterThanOrEqualTo(2);

        // These should NOT be errors
        issues.ShouldNotContain(i => i.Severity == ValidationSeverity.Error);
    }

    // ─── Multiple issues accumulated ──────────────────────────────────

    [Fact]
    public void Validate_MultipleIssues_AllReported()
    {
        var theme = MakeValidTheme();
        theme.Colors!.Primary = "ZZZZZZ";       // error: invalid hex
        theme.Docx!.BaseFontSize = -1;           // error: not positive
        theme.Docx.LineSpacing = 0;              // error: not positive
        theme.Docx.TableBorderWidth = -5;        // error: negative
        theme.Docx.Page!.Width = 0;              // error: not positive

        var issues = ThemeValidator.Validate(theme);

        issues.Count.ShouldBeGreaterThanOrEqualTo(5);
    }

    // ─── Edge case: errors and warnings for the same property ─────────
    // (A value of 0 for line spacing is an error; it should not ALSO produce a warning)

    [Fact]
    public void Validate_ZeroLineSpacing_ReturnsErrorOnly_NotWarning()
    {
        var theme = MakeValidTheme();
        theme.Docx!.LineSpacing = 0;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("LineSpacing", StringComparison.OrdinalIgnoreCase));

        // Should not also warn about unusual range for a value that's already an error
        issues.ShouldNotContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("LineSpacing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NegativeFontSize_ReturnsErrorOnly_NotWarning()
    {
        var theme = MakeValidTheme();
        theme.Docx!.BaseFontSize = -5;

        var issues = ThemeValidator.Validate(theme);

        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("BaseFontSize", StringComparison.OrdinalIgnoreCase));

        issues.ShouldNotContain(i =>
            i.Severity == ValidationSeverity.Warning &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("BaseFontSize", StringComparison.OrdinalIgnoreCase));
    }

    // ─── Property path specificity ────────────────────────────────────

    [Fact]
    public void Validate_ErrorPropertyPaths_IncludeSectionPrefix()
    {
        var theme = MakeValidTheme();
        theme.Colors!.Secondary = "ZZZZZZ";

        var issues = ThemeValidator.Validate(theme);

        // Property path should include the section, e.g. "Colors.Secondary"
        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("Colors", StringComparison.OrdinalIgnoreCase) &&
            i.PropertyPath.Contains("Secondary", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PagePropertyPath_IncludesFullPath()
    {
        var theme = MakeValidTheme();
        theme.Docx!.Page!.Width = 0;

        var issues = ThemeValidator.Validate(theme);

        // Path should be something like "Docx.Page.Width"
        issues.ShouldContain(i =>
            i.PropertyPath != null &&
            i.PropertyPath.Contains("Docx", StringComparison.OrdinalIgnoreCase) &&
            i.PropertyPath.Contains("Page", StringComparison.OrdinalIgnoreCase) &&
            i.PropertyPath.Contains("Width", StringComparison.OrdinalIgnoreCase));
    }

    // ─── PPTX section validation ─────────────────────────────────────

    [Fact]
    public void Validate_ValidPptxSection_ReturnsNoErrors()
    {
        var theme = MakeValidTheme();
        theme.Pptx = new ThemePptxSection
        {
            SlideSize = "16:9",
            BaseFontSize = 24,
            Heading1Size = 44,
            Heading2Size = 36,
            Heading3Size = 28,
            Background = new ThemePptxBackgroundSection { Color = "011627" },
            TitleSlide = new ThemePptxTitleSlideSection
            {
                TitleSize = 54,
                SubtitleSize = 28,
                BackgroundColor = "011627"
            },
        };

        var issues = ThemeValidator.Validate(theme);
        issues.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_PptxNullSection_NoErrors()
    {
        var theme = MakeValidTheme();
        theme.Pptx = null;

        var issues = ThemeValidator.Validate(theme);
        issues.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_PptxInvalidSlideSize_ReturnsError()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection { SlideSize = "3:2" }
        };

        var issues = ThemeValidator.Validate(theme);
        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath == "Pptx.SlideSize");
    }

    [Theory]
    [InlineData("16:9")]
    [InlineData("4:3")]
    [InlineData("16:10")]
    public void Validate_PptxValidSlideSize_NoErrors(string size)
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection { SlideSize = size }
        };

        var issues = ThemeValidator.Validate(theme);
        issues.ShouldNotContain(i => i.PropertyPath == "Pptx.SlideSize");
    }

    [Fact]
    public void Validate_PptxNegativeFontSize_ReturnsError()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection { BaseFontSize = -1 }
        };

        var issues = ThemeValidator.Validate(theme);
        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath == "Pptx.BaseFontSize");
    }

    [Fact]
    public void Validate_PptxInvalidBackgroundColor_ReturnsError()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection
            {
                Background = new ThemePptxBackgroundSection { Color = "ZZZZZZ" }
            }
        };

        var issues = ThemeValidator.Validate(theme);
        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath == "Pptx.Background.Color");
    }

    [Fact]
    public void Validate_PptxPerFormatColors_Validated()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection
            {
                Colors = new ThemeColorsSection { BodyText = "ZZZZZZ" }
            }
        };

        var issues = ThemeValidator.Validate(theme);
        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath != null &&
            i.PropertyPath.Contains("Pptx.Colors"));
    }

    [Fact]
    public void Validate_PptxChartPaletteInvalidColor_ReturnsError()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection
            {
                ChartPalette = new List<string> { "AABBCC", "ZZZZZZ" }
            }
        };

        var issues = ThemeValidator.Validate(theme);
        issues.ShouldContain(i =>
            i.Severity == ValidationSeverity.Error &&
            i.PropertyPath == "Pptx.ChartPalette[1]");
    }
}
