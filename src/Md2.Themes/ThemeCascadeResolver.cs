// agent-notes: { ctx: "4-layer cascade: CLI > theme > preset > template", deps: [ThemeDefinition.cs, PresetRegistry.cs, ResolvedTheme.cs], state: active, last: "sato@2026-03-12" }

using Md2.Core.Pipeline;

namespace Md2.Themes;

/// <summary>
/// Identifies which cascade layer a resolved property came from.
/// </summary>
public enum CascadeLayer
{
    Preset,
    Template,
    Theme,
    Cli
}

/// <summary>
/// Input to the cascade resolver. All fields optional — missing layers are skipped.
/// </summary>
public class ThemeCascadeInput
{
    /// <summary>Layer 4 (highest priority): CLI --style overrides.</summary>
    public ThemeDefinition? CliOverrides { get; set; }

    /// <summary>Layer 3: theme.yaml from --theme flag.</summary>
    public ThemeDefinition? Theme { get; set; }

    /// <summary>Layer 2: preset name (defaults to "default").</summary>
    public string PresetName { get; set; } = "default";

    /// <summary>Layer 1 (lowest priority): extracted template styles.</summary>
    public ThemeDefinition? Template { get; set; }
}

/// <summary>
/// Records where a specific property value came from in the cascade.
/// </summary>
public record CascadeTraceEntry(string Property, string Value, CascadeLayer Source);

/// <summary>
/// Resolves a ResolvedTheme by deep-merging 4 layers: CLI > theme > preset > template.
/// Each property is resolved independently — the first layer (highest priority) that
/// defines a value wins.
/// </summary>
public static class ThemeCascadeResolver
{
    /// <summary>
    /// Resolves cascade layers into a ResolvedTheme.
    /// </summary>
    public static ResolvedTheme Resolve(ThemeCascadeInput input)
    {
        return ResolveWithTrace(input).Theme;
    }

    /// <summary>
    /// Resolves cascade layers and returns both the theme and a trace of where each property came from.
    /// </summary>
    public static (ResolvedTheme Theme, IReadOnlyList<CascadeTraceEntry> Trace) ResolveWithTrace(ThemeCascadeInput input)
    {
        var preset = PresetRegistry.GetPreset(input.PresetName);

        // Build layer stack: highest priority first
        var layers = new List<(ThemeDefinition Def, CascadeLayer Layer)>();
        if (input.CliOverrides is not null) layers.Add((input.CliOverrides, CascadeLayer.Cli));
        if (input.Theme is not null) layers.Add((input.Theme, CascadeLayer.Theme));
        layers.Add((preset, CascadeLayer.Preset));
        if (input.Template is not null) layers.Add((input.Template, CascadeLayer.Template));

        var trace = new List<CascadeTraceEntry>();
        var theme = new ResolvedTheme();

        // Typography
        theme.HeadingFont = ResolveString(layers, d => d.Typography?.HeadingFont, "HeadingFont", trace);
        theme.BodyFont = ResolveString(layers, d => d.Typography?.BodyFont, "BodyFont", trace);
        theme.MonoFont = ResolveString(layers, d => d.Typography?.MonoFont, "MonoFont", trace);
        theme.MonoFontFallback = ResolveString(layers, d => d.Typography?.MonoFontFallback, "MonoFontFallback", trace);

        // Colors
        theme.PrimaryColor = ResolveString(layers, d => d.Colors?.Primary, "PrimaryColor", trace);
        theme.SecondaryColor = ResolveString(layers, d => d.Colors?.Secondary, "SecondaryColor", trace);
        theme.BodyTextColor = ResolveString(layers, d => d.Colors?.BodyText, "BodyTextColor", trace);
        theme.CodeBackgroundColor = ResolveString(layers, d => d.Colors?.CodeBackground, "CodeBackgroundColor", trace);
        theme.CodeBlockBorderColor = ResolveString(layers, d => d.Colors?.CodeBorder, "CodeBlockBorderColor", trace);
        theme.LinkColor = ResolveString(layers, d => d.Colors?.Link, "LinkColor", trace);
        theme.TableHeaderBackground = ResolveString(layers, d => d.Colors?.TableHeaderBackground, "TableHeaderBackground", trace);
        theme.TableHeaderForeground = ResolveString(layers, d => d.Colors?.TableHeaderForeground, "TableHeaderForeground", trace);
        theme.TableBorderColor = ResolveString(layers, d => d.Colors?.TableBorder, "TableBorderColor", trace);
        theme.TableAlternateRowBackground = ResolveString(layers, d => d.Colors?.TableAlternateRow, "TableAlternateRowBackground", trace);
        theme.BlockquoteBorderColor = ResolveString(layers, d => d.Colors?.BlockquoteBorder, "BlockquoteBorderColor", trace);
        theme.BlockquoteTextColor = ResolveString(layers, d => d.Colors?.BlockquoteText, "BlockquoteTextColor", trace);

        // Font sizes
        theme.BaseFontSize = ResolveDouble(layers, d => d.Docx?.BaseFontSize, "BaseFontSize", trace);
        theme.Heading1Size = ResolveDouble(layers, d => d.Docx?.Heading1Size, "Heading1Size", trace);
        theme.Heading2Size = ResolveDouble(layers, d => d.Docx?.Heading2Size, "Heading2Size", trace);
        theme.Heading3Size = ResolveDouble(layers, d => d.Docx?.Heading3Size, "Heading3Size", trace);
        theme.Heading4Size = ResolveDouble(layers, d => d.Docx?.Heading4Size, "Heading4Size", trace);
        theme.Heading5Size = ResolveDouble(layers, d => d.Docx?.Heading5Size, "Heading5Size", trace);
        theme.Heading6Size = ResolveDouble(layers, d => d.Docx?.Heading6Size, "Heading6Size", trace);
        theme.LineSpacing = ResolveDouble(layers, d => d.Docx?.LineSpacing, "LineSpacing", trace);

        // Table / blockquote
        theme.TableBorderWidth = ResolveInt(layers, d => d.Docx?.TableBorderWidth, "TableBorderWidth", trace);
        theme.BlockquoteIndentTwips = ResolveInt(layers, d => d.Docx?.BlockquoteIndentTwips, "BlockquoteIndentTwips", trace);

        // Page layout
        theme.PageWidth = ResolveUint(layers, d => d.Docx?.Page?.Width, "PageWidth", trace);
        theme.PageHeight = ResolveUint(layers, d => d.Docx?.Page?.Height, "PageHeight", trace);
        theme.MarginTop = ResolveInt(layers, d => d.Docx?.Page?.MarginTop, "MarginTop", trace);
        theme.MarginBottom = ResolveInt(layers, d => d.Docx?.Page?.MarginBottom, "MarginBottom", trace);
        theme.MarginLeft = ResolveInt(layers, d => d.Docx?.Page?.MarginLeft, "MarginLeft", trace);
        theme.MarginRight = ResolveInt(layers, d => d.Docx?.Page?.MarginRight, "MarginRight", trace);

        return (theme, trace);
    }

    private static string ResolveString(
        List<(ThemeDefinition Def, CascadeLayer Layer)> layers,
        Func<ThemeDefinition, string?> selector,
        string propertyName,
        List<CascadeTraceEntry> trace)
    {
        foreach (var (def, layer) in layers)
        {
            var value = selector(def);
            if (value is not null)
            {
                trace.Add(new CascadeTraceEntry(propertyName, value, layer));
                return value;
            }
        }

        // Fallback to ResolvedTheme default (should never happen if preset is complete)
        var fallback = new ResolvedTheme();
        var prop = typeof(ResolvedTheme).GetProperty(propertyName);
        var defaultValue = prop?.GetValue(fallback)?.ToString() ?? "";
        trace.Add(new CascadeTraceEntry(propertyName, defaultValue, CascadeLayer.Preset));
        return defaultValue;
    }

    private static double ResolveDouble(
        List<(ThemeDefinition Def, CascadeLayer Layer)> layers,
        Func<ThemeDefinition, double?> selector,
        string propertyName,
        List<CascadeTraceEntry> trace)
    {
        foreach (var (def, layer) in layers)
        {
            var value = selector(def);
            if (value.HasValue)
            {
                trace.Add(new CascadeTraceEntry(propertyName, value.Value.ToString(), layer));
                return value.Value;
            }
        }

        var fallback = new ResolvedTheme();
        var prop = typeof(ResolvedTheme).GetProperty(propertyName);
        var defaultValue = prop != null ? (double)prop.GetValue(fallback)! : 0;
        trace.Add(new CascadeTraceEntry(propertyName, defaultValue.ToString(), CascadeLayer.Preset));
        return defaultValue;
    }

    private static int ResolveInt(
        List<(ThemeDefinition Def, CascadeLayer Layer)> layers,
        Func<ThemeDefinition, int?> selector,
        string propertyName,
        List<CascadeTraceEntry> trace)
    {
        foreach (var (def, layer) in layers)
        {
            var value = selector(def);
            if (value.HasValue)
            {
                trace.Add(new CascadeTraceEntry(propertyName, value.Value.ToString(), layer));
                return value.Value;
            }
        }

        var fallback = new ResolvedTheme();
        var prop = typeof(ResolvedTheme).GetProperty(propertyName);
        var defaultValue = prop != null ? (int)prop.GetValue(fallback)! : 0;
        trace.Add(new CascadeTraceEntry(propertyName, defaultValue.ToString(), CascadeLayer.Preset));
        return defaultValue;
    }

    private static uint ResolveUint(
        List<(ThemeDefinition Def, CascadeLayer Layer)> layers,
        Func<ThemeDefinition, uint?> selector,
        string propertyName,
        List<CascadeTraceEntry> trace)
    {
        foreach (var (def, layer) in layers)
        {
            var value = selector(def);
            if (value.HasValue)
            {
                trace.Add(new CascadeTraceEntry(propertyName, value.Value.ToString(), layer));
                return value.Value;
            }
        }

        var fallback = new ResolvedTheme();
        var prop = typeof(ResolvedTheme).GetProperty(propertyName);
        var defaultValue = prop != null ? (uint)prop.GetValue(fallback)! : 0u;
        trace.Add(new CascadeTraceEntry(propertyName, defaultValue.ToString(), CascadeLayer.Preset));
        return defaultValue;
    }
}
