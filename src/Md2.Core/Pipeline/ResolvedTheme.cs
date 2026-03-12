// agent-notes: { ctx: "Resolved theme with font/color/spacing/table defaults", deps: [], state: active, last: "sato@2026-03-11" }

namespace Md2.Core.Pipeline;

public class ResolvedTheme
{
    // Fonts
    public string HeadingFont { get; set; } = "Calibri";
    public string BodyFont { get; set; } = "Cambria";
    public string MonoFont { get; set; } = "Cascadia Code";
    public string MonoFontFallback { get; set; } = "Consolas";

    // Colors (hex without #)
    public string PrimaryColor { get; set; } = "1B3A5C";
    public string SecondaryColor { get; set; } = "4A90D9";
    public string BodyTextColor { get; set; } = "333333";
    public string CodeBackgroundColor { get; set; } = "F5F5F5";
    public string CodeBlockBorderColor { get; set; } = "E0E0E0";
    public string LinkColor { get; set; } = "4A90D9";

    // Font sizes in points
    public double BaseFontSize { get; set; } = 11.0;
    public double Heading1Size { get; set; } = 28.0;
    public double Heading2Size { get; set; } = 22.0;
    public double Heading3Size { get; set; } = 16.0;
    public double Heading4Size { get; set; } = 13.0;
    public double Heading5Size { get; set; } = 11.0;
    public double Heading6Size { get; set; } = 11.0;

    // Table styling
    public string TableHeaderBackground { get; set; } = "1B3A5C";
    public string TableHeaderForeground { get; set; } = "FFFFFF";
    public string TableBorderColor { get; set; } = "BFBFBF";
    public int TableBorderWidth { get; set; } = 4; // in eighth-points
    public string TableAlternateRowBackground { get; set; } = "F2F2F2";

    // Blockquote styling
    public string BlockquoteBorderColor { get; set; } = "4A90D9";
    public string BlockquoteTextColor { get; set; } = "555555";
    public int BlockquoteIndentTwips { get; set; } = 720; // 0.5 inches per nesting level

    // Line spacing (multiplier, e.g. 1.15)
    public double LineSpacing { get; set; } = 1.15;

    // Page layout (in twentieths of a point)
    public uint PageWidth { get; set; } = 11906;   // A4 width
    public uint PageHeight { get; set; } = 16838;   // A4 height
    public int MarginTop { get; set; } = 1440;      // 1 inch
    public int MarginBottom { get; set; } = 1440;   // 1 inch
    public int MarginLeft { get; set; } = 1800;     // 1.25 inches
    public int MarginRight { get; set; } = 1800;    // 1.25 inches

    /// <summary>
    /// Returns the heading font size in points for a given heading level (1-6).
    /// </summary>
    public double GetHeadingSize(int level) => level switch
    {
        1 => Heading1Size,
        2 => Heading2Size,
        3 => Heading3Size,
        4 => Heading4Size,
        5 => Heading5Size,
        6 => Heading6Size,
        _ => BaseFontSize
    };

    /// <summary>
    /// Creates a ResolvedTheme with sensible defaults.
    /// </summary>
    public static ResolvedTheme CreateDefault() => new();
}
