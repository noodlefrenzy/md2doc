// agent-notes: { ctx: "4-layer cascade: CLI > theme > preset > template, with PPTX sub-object", deps: [ThemeDefinition.cs, PresetRegistry.cs, ResolvedTheme.cs, ResolvedPptxTheme.cs], state: active, last: "sato@2026-03-15" }

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
        var d = new ResolvedTheme(); // compile-time fallback defaults
        var theme = new ResolvedTheme();

        // Typography
        theme.HeadingFont = ResolveString(layers, t => t.Typography?.HeadingFont, "HeadingFont", d.HeadingFont, trace);
        theme.BodyFont = ResolveString(layers, t => t.Typography?.BodyFont, "BodyFont", d.BodyFont, trace);
        theme.MonoFont = ResolveString(layers, t => t.Typography?.MonoFont, "MonoFont", d.MonoFont, trace);
        theme.MonoFontFallback = ResolveString(layers, t => t.Typography?.MonoFontFallback, "MonoFontFallback", d.MonoFontFallback, trace);

        // Colors
        theme.PrimaryColor = ResolveString(layers, t => t.Colors?.Primary, "PrimaryColor", d.PrimaryColor, trace);
        theme.SecondaryColor = ResolveString(layers, t => t.Colors?.Secondary, "SecondaryColor", d.SecondaryColor, trace);
        theme.BodyTextColor = ResolveString(layers, t => t.Colors?.BodyText, "BodyTextColor", d.BodyTextColor, trace);
        theme.CodeBackgroundColor = ResolveString(layers, t => t.Colors?.CodeBackground, "CodeBackgroundColor", d.CodeBackgroundColor, trace);
        theme.CodeBlockBorderColor = ResolveString(layers, t => t.Colors?.CodeBorder, "CodeBlockBorderColor", d.CodeBlockBorderColor, trace);
        theme.LinkColor = ResolveString(layers, t => t.Colors?.Link, "LinkColor", d.LinkColor, trace);
        theme.TableHeaderBackground = ResolveString(layers, t => t.Colors?.TableHeaderBackground, "TableHeaderBackground", d.TableHeaderBackground, trace);
        theme.TableHeaderForeground = ResolveString(layers, t => t.Colors?.TableHeaderForeground, "TableHeaderForeground", d.TableHeaderForeground, trace);
        theme.TableBorderColor = ResolveString(layers, t => t.Colors?.TableBorder, "TableBorderColor", d.TableBorderColor, trace);
        theme.TableAlternateRowBackground = ResolveString(layers, t => t.Colors?.TableAlternateRow, "TableAlternateRowBackground", d.TableAlternateRowBackground, trace);
        theme.BlockquoteBorderColor = ResolveString(layers, t => t.Colors?.BlockquoteBorder, "BlockquoteBorderColor", d.BlockquoteBorderColor, trace);
        theme.BlockquoteTextColor = ResolveString(layers, t => t.Colors?.BlockquoteText, "BlockquoteTextColor", d.BlockquoteTextColor, trace);

        // Font sizes
        theme.BaseFontSize = Resolve(layers, t => t.Docx?.BaseFontSize, "BaseFontSize", d.BaseFontSize, trace);
        theme.Heading1Size = Resolve(layers, t => t.Docx?.Heading1Size, "Heading1Size", d.Heading1Size, trace);
        theme.Heading2Size = Resolve(layers, t => t.Docx?.Heading2Size, "Heading2Size", d.Heading2Size, trace);
        theme.Heading3Size = Resolve(layers, t => t.Docx?.Heading3Size, "Heading3Size", d.Heading3Size, trace);
        theme.Heading4Size = Resolve(layers, t => t.Docx?.Heading4Size, "Heading4Size", d.Heading4Size, trace);
        theme.Heading5Size = Resolve(layers, t => t.Docx?.Heading5Size, "Heading5Size", d.Heading5Size, trace);
        theme.Heading6Size = Resolve(layers, t => t.Docx?.Heading6Size, "Heading6Size", d.Heading6Size, trace);
        theme.LineSpacing = Resolve(layers, t => t.Docx?.LineSpacing, "LineSpacing", d.LineSpacing, trace);

        // Table / blockquote
        theme.TableBorderWidth = Resolve(layers, t => t.Docx?.TableBorderWidth, "TableBorderWidth", d.TableBorderWidth, trace);
        theme.BlockquoteIndentTwips = Resolve(layers, t => t.Docx?.BlockquoteIndentTwips, "BlockquoteIndentTwips", d.BlockquoteIndentTwips, trace);

        // Page layout
        theme.PageWidth = Resolve(layers, t => t.Docx?.Page?.Width, "PageWidth", d.PageWidth, trace);
        theme.PageHeight = Resolve(layers, t => t.Docx?.Page?.Height, "PageHeight", d.PageHeight, trace);
        theme.MarginTop = Resolve(layers, t => t.Docx?.Page?.MarginTop, "MarginTop", d.MarginTop, trace);
        theme.MarginBottom = Resolve(layers, t => t.Docx?.Page?.MarginBottom, "MarginBottom", d.MarginBottom, trace);
        theme.MarginLeft = Resolve(layers, t => t.Docx?.Page?.MarginLeft, "MarginLeft", d.MarginLeft, trace);
        theme.MarginRight = Resolve(layers, t => t.Docx?.Page?.MarginRight, "MarginRight", d.MarginRight, trace);

        // PPTX sub-object (ADR-0016: populated when any layer has pptx: section)
        theme.Pptx = ResolvePptx(layers, theme, trace);

        return (theme, trace);
    }

    private static T Resolve<T>(
        List<(ThemeDefinition Def, CascadeLayer Layer)> layers,
        Func<ThemeDefinition, T?> selector,
        string propertyName,
        T fallbackValue,
        List<CascadeTraceEntry> trace) where T : struct
    {
        foreach (var (def, layer) in layers)
        {
            var value = selector(def);
            if (value.HasValue)
            {
                trace.Add(new CascadeTraceEntry(propertyName, value.Value.ToString()!, layer));
                return value.Value;
            }
        }

        trace.Add(new CascadeTraceEntry(propertyName, fallbackValue.ToString()!, CascadeLayer.Preset));
        return fallbackValue;
    }

    private static string ResolveString(
        List<(ThemeDefinition Def, CascadeLayer Layer)> layers,
        Func<ThemeDefinition, string?> selector,
        string propertyName,
        string fallbackValue,
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

        trace.Add(new CascadeTraceEntry(propertyName, fallbackValue, CascadeLayer.Preset));
        return fallbackValue;
    }

    /// <summary>
    /// Resolves the PPTX sub-object from cascade layers.
    /// Per ADR-0016: per-format color overrides take precedence over shared colors.
    /// Cascade: CLI pptx > theme pptx > preset pptx > defaults.
    /// </summary>
    private static ResolvedPptxTheme ResolvePptx(
        List<(ThemeDefinition Def, CascadeLayer Layer)> layers,
        ResolvedTheme sharedTheme,
        List<CascadeTraceEntry> trace)
    {
        var dp = new ResolvedPptxTheme();
        var pptx = new ResolvedPptxTheme();

        // Font sizes
        pptx.BaseFontSize = Resolve(layers, t => t.Pptx?.BaseFontSize, "Pptx.BaseFontSize", dp.BaseFontSize, trace);
        pptx.Heading1Size = Resolve(layers, t => t.Pptx?.Heading1Size, "Pptx.Heading1Size", dp.Heading1Size, trace);
        pptx.Heading2Size = Resolve(layers, t => t.Pptx?.Heading2Size, "Pptx.Heading2Size", dp.Heading2Size, trace);
        pptx.Heading3Size = Resolve(layers, t => t.Pptx?.Heading3Size, "Pptx.Heading3Size", dp.Heading3Size, trace);

        // Slide size
        var slideSizeStr = ResolveStringOptional(layers, t => t.Pptx?.SlideSize, "Pptx.SlideSize", "16:9", trace);
        pptx.SlideSize = ParseSlideSize(slideSizeStr);

        // Background
        pptx.BackgroundColor = ResolveString(layers, t => t.Pptx?.Background?.Color, "Pptx.BackgroundColor", dp.BackgroundColor, trace);
        pptx.BackgroundImage = ResolveStringOptional(layers, t => t.Pptx?.Background?.Image, "Pptx.BackgroundImage", null, trace);

        // Per-format color overrides (ADR-0016 Wei debate)
        // pptx.colors overrides shared colors for PPTX output
        pptx.BodyTextColor = ResolveStringOptional(layers, t => t.Pptx?.Colors?.BodyText, "Pptx.BodyTextColor", null, trace);
        pptx.PrimaryColor = ResolveStringOptional(layers, t => t.Pptx?.Colors?.Primary, "Pptx.PrimaryColor", null, trace);
        pptx.SecondaryColor = ResolveStringOptional(layers, t => t.Pptx?.Colors?.Secondary, "Pptx.SecondaryColor", null, trace);

        // Title slide layout
        pptx.TitleSlide = new ResolvedSlideLayoutTheme
        {
            TitleSize = Resolve(layers, t => t.Pptx?.TitleSlide?.TitleSize, "Pptx.TitleSlide.TitleSize", dp.TitleSlide.TitleSize, trace),
            SubtitleSize = ResolveOptional(layers, t => t.Pptx?.TitleSlide?.SubtitleSize, "Pptx.TitleSlide.SubtitleSize", dp.TitleSlide.SubtitleSize, trace),
            BackgroundColor = ResolveStringOptional(layers, t => t.Pptx?.TitleSlide?.BackgroundColor, "Pptx.TitleSlide.BackgroundColor", null, trace),
        };

        // Section divider layout
        pptx.SectionDivider = new ResolvedSlideLayoutTheme
        {
            TitleSize = Resolve(layers, t => t.Pptx?.SectionDivider?.TitleSize, "Pptx.SectionDivider.TitleSize", dp.SectionDivider.TitleSize, trace),
            BackgroundColor = ResolveStringOptional(layers, t => t.Pptx?.SectionDivider?.BackgroundColor, "Pptx.SectionDivider.BackgroundColor", null, trace),
        };

        // Content layout
        pptx.Content = new ResolvedSlideLayoutTheme
        {
            TitleSize = Resolve(layers, t => t.Pptx?.Content?.TitleSize, "Pptx.Content.TitleSize", dp.Content.TitleSize, trace),
            BodySize = Resolve(layers, t => t.Pptx?.Content?.BodySize, "Pptx.Content.BodySize", dp.Content.BodySize, trace),
            BulletIndent = Resolve(layers, t => t.Pptx?.Content?.BulletIndent, "Pptx.Content.BulletIndent", dp.Content.BulletIndent, trace),
        };

        // Two-column layout
        pptx.TwoColumn = new ResolvedTwoColumnTheme
        {
            Gutter = Resolve(layers, t => t.Pptx?.TwoColumn?.Gutter, "Pptx.TwoColumn.Gutter", dp.TwoColumn.Gutter, trace),
        };

        // Chart palette
        foreach (var (def, layer) in layers)
        {
            if (def.Pptx?.ChartPalette is { Count: > 0 } palette)
            {
                pptx.ChartPalette = palette.AsReadOnly();
                trace.Add(new CascadeTraceEntry("Pptx.ChartPalette", $"[{palette.Count} colors]", layer));
                break;
            }
        }

        // Code block
        pptx.CodeBlock = new ResolvedCodeBlockTheme
        {
            FontSize = Resolve(layers, t => t.Pptx?.CodeBlock?.FontSize, "Pptx.CodeBlock.FontSize", dp.CodeBlock.FontSize, trace),
            Padding = Resolve(layers, t => t.Pptx?.CodeBlock?.Padding, "Pptx.CodeBlock.Padding", dp.CodeBlock.Padding, trace),
            BorderRadius = Resolve(layers, t => t.Pptx?.CodeBlock?.BorderRadius, "Pptx.CodeBlock.BorderRadius", dp.CodeBlock.BorderRadius, trace),
        };

        return pptx;
    }

    private static string? ResolveStringOptional(
        List<(ThemeDefinition Def, CascadeLayer Layer)> layers,
        Func<ThemeDefinition, string?> selector,
        string propertyName,
        string? fallbackValue,
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

        if (fallbackValue is not null)
            trace.Add(new CascadeTraceEntry(propertyName, fallbackValue, CascadeLayer.Preset));
        return fallbackValue;
    }

    private static double? ResolveOptional(
        List<(ThemeDefinition Def, CascadeLayer Layer)> layers,
        Func<ThemeDefinition, double?> selector,
        string propertyName,
        double? fallbackValue,
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

        if (fallbackValue.HasValue)
            trace.Add(new CascadeTraceEntry(propertyName, fallbackValue.Value.ToString(), CascadeLayer.Preset));
        return fallbackValue;
    }

    private static Core.Slides.SlideSize ParseSlideSize(string value) => value switch
    {
        "16:9" => Core.Slides.SlideSize.Widescreen16x9,
        "4:3" => Core.Slides.SlideSize.Standard4x3,
        "16:10" => Core.Slides.SlideSize.Widescreen16x10,
        _ => Core.Slides.SlideSize.Widescreen16x9
    };
}
