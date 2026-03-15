// agent-notes: { ctx: "Parse MARP image syntax extensions (bg, w:, h:, fit, cover, etc.)", deps: [], state: active, last: "sato@2026-03-15" }

using System.Text.RegularExpressions;

namespace Md2.Slides;

/// <summary>
/// Parses MARP extended image syntax from alt text.
/// Examples: ![bg cover](img.jpg), ![w:200 h:100](img.jpg), ![bg left:30%](img.jpg)
/// </summary>
public record MarpImageInfo
{
    /// <summary>Whether this is a background image (alt text contains "bg").</summary>
    public bool IsBackground { get; init; }

    /// <summary>Background sizing mode: cover, contain, fit, auto, or null for default.</summary>
    public string? BackgroundSize { get; init; }

    /// <summary>Split layout: "left" or "right", with optional percentage.</summary>
    public string? SplitDirection { get; init; }

    /// <summary>Split percentage (e.g., 30 for left:30%). Null if no split.</summary>
    public int? SplitPercent { get; init; }

    /// <summary>Explicit width (e.g., "200px", "50%", "10em").</summary>
    public string? Width { get; init; }

    /// <summary>Explicit height (e.g., "100px", "50%").</summary>
    public string? Height { get; init; }

    /// <summary>Whether fit keyword is present (auto-scale to slide).</summary>
    public bool Fit { get; init; }

    /// <summary>Any remaining alt text not parsed as MARP keywords.</summary>
    public string? RemainingAlt { get; init; }
}

/// <summary>
/// Parser for MARP extended image syntax in Markdown image alt text.
/// </summary>
public static partial class MarpImageSyntax
{
    [GeneratedRegex(@"w:(\d+(?:px|em|%)?)", RegexOptions.IgnoreCase)]
    private static partial Regex WidthRegex();

    [GeneratedRegex(@"h:(\d+(?:px|em|%)?)", RegexOptions.IgnoreCase)]
    private static partial Regex HeightRegex();

    [GeneratedRegex(@"(left|right)(?::(\d+)%)?", RegexOptions.IgnoreCase)]
    private static partial Regex SplitRegex();

    private static readonly HashSet<string> BackgroundSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        "cover", "contain", "fit", "auto"
    };

    /// <summary>
    /// Parse MARP image alt text into structured image info.
    /// Returns null if the alt text contains no MARP-specific keywords.
    /// </summary>
    public static MarpImageInfo? Parse(string? altText)
    {
        if (string.IsNullOrWhiteSpace(altText))
            return null;

        var tokens = altText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var isBackground = false;
        string? bgSize = null;
        string? splitDir = null;
        int? splitPct = null;
        string? width = null;
        string? height = null;
        var fit = false;
        var remaining = new List<string>();

        foreach (var token in tokens)
        {
            if (string.Equals(token, "bg", StringComparison.OrdinalIgnoreCase))
            {
                isBackground = true;
                continue;
            }

            if (BackgroundSizes.Contains(token))
            {
                if (isBackground)
                {
                    bgSize = token.ToLowerInvariant();
                    continue;
                }
                // "fit" without "bg" is the fit keyword for headings
                if (string.Equals(token, "fit", StringComparison.OrdinalIgnoreCase))
                {
                    fit = true;
                    continue;
                }
            }

            if (string.Equals(token, "fit", StringComparison.OrdinalIgnoreCase))
            {
                fit = true;
                continue;
            }

            var widthMatch = WidthRegex().Match(token);
            if (widthMatch.Success)
            {
                width = widthMatch.Groups[1].Value;
                continue;
            }

            var heightMatch = HeightRegex().Match(token);
            if (heightMatch.Success)
            {
                height = heightMatch.Groups[1].Value;
                continue;
            }

            var splitMatch = SplitRegex().Match(token);
            if (splitMatch.Success && isBackground)
            {
                splitDir = splitMatch.Groups[1].Value.ToLowerInvariant();
                if (splitMatch.Groups[2].Success)
                    splitPct = int.Parse(splitMatch.Groups[2].Value);
                continue;
            }

            remaining.Add(token);
        }

        // If nothing MARP-specific was found, return null
        if (!isBackground && !fit && width == null && height == null)
            return null;

        return new MarpImageInfo
        {
            IsBackground = isBackground,
            BackgroundSize = bgSize,
            SplitDirection = splitDir,
            SplitPercent = splitPct,
            Width = width,
            Height = height,
            Fit = fit,
            RemainingAlt = remaining.Count > 0 ? string.Join(" ", remaining) : null
        };
    }
}
