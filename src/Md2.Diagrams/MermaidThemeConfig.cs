// agent-notes: { ctx: "DTO mapping ResolvedTheme to Mermaid themeVariables", deps: [Md2.Core.Pipeline.ResolvedTheme], state: active, last: "sato@2026-03-13" }

using System.Security.Cryptography;
using System.Text;
using Md2.Core.Pipeline;

namespace Md2.Diagrams;

/// <summary>
/// Maps the subset of <see cref="ResolvedTheme"/> properties relevant to Mermaid diagram rendering.
/// Used to inject themeVariables into mermaid.initialize() and to differentiate cache keys by theme.
/// </summary>
public class MermaidThemeConfig
{
    public string PrimaryColor { get; init; } = "1B3A5C";
    public string SecondaryColor { get; init; } = "4A90D9";
    public string TextColor { get; init; } = "333333";
    public string PrimaryTextColor { get; init; } = "FFFFFF";
    public string BackgroundColor { get; init; } = "F5F5F5";
    public string BorderColor { get; init; } = "E0E0E0";
    public string ClusterBackground { get; init; } = "F2F2F2";
    public string FontFamily { get; init; } = "Calibri, sans-serif";
    public double FontSizePx { get; init; } = 14.67;

    /// <summary>
    /// Creates a <see cref="MermaidThemeConfig"/> from a <see cref="ResolvedTheme"/>,
    /// mapping document theme properties to Mermaid themeVariables.
    /// </summary>
    public static MermaidThemeConfig FromResolvedTheme(ResolvedTheme theme)
    {
        var primaryTextColor = theme.TableHeaderForeground;
        // Auto-derive contrast if primary color is extreme
        if (string.IsNullOrEmpty(primaryTextColor) || primaryTextColor == "FFFFFF")
        {
            primaryTextColor = DeriveContrastTextColor(theme.PrimaryColor);
        }

        var fontFamily = theme.HeadingFont;
        if (!fontFamily.Contains("sans-serif", StringComparison.OrdinalIgnoreCase))
            fontFamily += ", sans-serif";

        return new MermaidThemeConfig
        {
            PrimaryColor = theme.PrimaryColor,
            SecondaryColor = theme.SecondaryColor,
            TextColor = theme.BodyTextColor,
            PrimaryTextColor = primaryTextColor,
            BackgroundColor = theme.CodeBackgroundColor,
            BorderColor = theme.CodeBlockBorderColor,
            ClusterBackground = theme.TableAlternateRowBackground,
            FontFamily = fontFamily,
            FontSizePx = theme.BaseFontSize * (96.0 / 72.0),
        };
    }

    /// <summary>
    /// Produces a deterministic cache-key string from this config's properties.
    /// </summary>
    public string ToCacheKey()
    {
        var input = string.Join("|",
            PrimaryColor, SecondaryColor, TextColor, PrimaryTextColor,
            BackgroundColor, BorderColor, ClusterBackground,
            FontFamily, FontSizePx.ToString("F2"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Auto-derives a readable text color for the given background hex color.
    /// Dark backgrounds get light text; light backgrounds get dark text.
    /// Uses relative luminance per WCAG contrast guidelines.
    /// </summary>
    public static string DeriveContrastTextColor(string hexColor)
    {
        var r = Convert.ToInt32(hexColor[..2], 16);
        var g = Convert.ToInt32(hexColor[2..4], 16);
        var b = Convert.ToInt32(hexColor[4..6], 16);

        // Relative luminance (simplified sRGB)
        var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;

        // Dark background → light text, light background → dark text
        return luminance < 0.5 ? "F0F0F0" : "1A1A1A";
    }
}
