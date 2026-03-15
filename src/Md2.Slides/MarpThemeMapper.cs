// agent-notes: { ctx: "Map MARP theme: directive to md2 preset hint", deps: [], state: active, last: "sato@2026-03-15" }

namespace Md2.Slides;

/// <summary>
/// Maps MARP theme: directive values to md2 preset name hints.
/// Per ADR-0016: MARP themes are CSS-based and have no PPTX equivalent.
/// When no --preset or --theme is specified, the MARP theme: directive
/// is used as a hint to select an md2 preset.
/// </summary>
public static class MarpThemeMapper
{
    private static readonly Dictionary<string, string> ThemeToPreset = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = "default",
        ["gaia"] = "default",
        ["uncover"] = "minimal",
    };

    /// <summary>
    /// Returns the md2 preset name that best matches a MARP theme: directive value.
    /// Returns null if no mapping exists for the given theme name.
    /// </summary>
    public static string? MapToPreset(string? marpTheme)
    {
        if (marpTheme is null)
            return null;

        return ThemeToPreset.TryGetValue(marpTheme, out var preset) ? preset : null;
    }
}
