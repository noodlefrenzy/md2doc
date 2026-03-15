// agent-notes: { ctx: "Resolved PPTX theme sub-object per ADR-0016", deps: [Md2.Core.Slides.SlideSize], state: active, last: "sato@2026-03-15" }

using Md2.Core.Slides;

namespace Md2.Core.Pipeline;

/// <summary>
/// Resolved PPTX-specific theme properties. Sub-object of ResolvedTheme
/// per ADR-0016 (Wei debate: mandatory sub-object, not flat fields).
/// </summary>
public class ResolvedPptxTheme
{
    // Font sizes in points (PPTX base is larger than DOCX)
    public double BaseFontSize { get; set; } = 24.0;
    public double Heading1Size { get; set; } = 44.0;
    public double Heading2Size { get; set; } = 36.0;
    public double Heading3Size { get; set; } = 28.0;

    // Slide dimensions
    public SlideSize SlideSize { get; set; } = SlideSize.Widescreen16x9;

    // Default slide background (hex without #)
    public string BackgroundColor { get; set; } = "FFFFFF";
    public string? BackgroundImage { get; set; }

    // Per-format color overrides (merged from pptx.colors section)
    public string? BodyTextColor { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }

    // Per-layout theme settings
    public ResolvedSlideLayoutTheme TitleSlide { get; set; } = new()
    {
        TitleSize = 54.0,
        SubtitleSize = 28.0,
        BackgroundColor = null // falls back to slide background
    };

    public ResolvedSlideLayoutTheme SectionDivider { get; set; } = new()
    {
        TitleSize = 44.0,
        BackgroundColor = null
    };

    public ResolvedSlideLayoutTheme Content { get; set; } = new()
    {
        TitleSize = 36.0,
        BodySize = 24.0,
        BulletIndent = 36.0
    };

    public ResolvedTwoColumnTheme TwoColumn { get; set; } = new();

    // Chart palette for native PPTX charts
    public IReadOnlyList<string> ChartPalette { get; set; } = new[]
    {
        "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47"
    };

    // Code block styling
    public ResolvedCodeBlockTheme CodeBlock { get; set; } = new();

    /// <summary>
    /// Returns the heading font size in points for a given heading level (1-3).
    /// Levels 4-6 fall back to BaseFontSize.
    /// </summary>
    public double GetHeadingSize(int level) => level switch
    {
        1 => Heading1Size,
        2 => Heading2Size,
        3 => Heading3Size,
        _ => BaseFontSize
    };

    /// <summary>
    /// Creates a ResolvedPptxTheme with sensible defaults.
    /// </summary>
    public static ResolvedPptxTheme CreateDefault() => new();
}

/// <summary>
/// Theme settings for a specific slide layout type.
/// </summary>
public class ResolvedSlideLayoutTheme
{
    public double TitleSize { get; set; } = 36.0;
    public double? SubtitleSize { get; set; }
    public double BodySize { get; set; } = 24.0;
    public double BulletIndent { get; set; } = 36.0;
    public string? BackgroundColor { get; set; }
}

/// <summary>
/// Theme settings for two-column layout.
/// </summary>
public class ResolvedTwoColumnTheme
{
    public double Gutter { get; set; } = 48.0; // points between columns
}

/// <summary>
/// Theme settings for code blocks in PPTX.
/// </summary>
public class ResolvedCodeBlockTheme
{
    public double FontSize { get; set; } = 14.0;
    public double Padding { get; set; } = 12.0;  // points
    public double BorderRadius { get; set; } = 8.0; // points
}
