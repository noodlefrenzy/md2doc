// agent-notes: { ctx: "Parsed MARP directive value object", deps: [], state: active, last: "sato@2026-03-15" }

namespace Md2.Slides.Directives;

/// <summary>
/// A single parsed MARP directive extracted from an HTML comment or front matter.
/// </summary>
public record MarpDirective(string Key, string Value, MarpDirectiveScope Scope)
{
    /// <summary>
    /// The zero-based slide index where this directive was found.
    /// -1 for front-matter (global) directives.
    /// </summary>
    public int SlideIndex { get; init; } = -1;
}

/// <summary>
/// Scope of a MARP directive per Marpit v3.x semantics.
/// </summary>
public enum MarpDirectiveScope
{
    /// <summary>Global: applies to all slides (from front matter).</summary>
    Global,

    /// <summary>Local: applies from current slide forward until overridden.</summary>
    Local,

    /// <summary>Scoped: applies only to the current slide (underscore-prefixed).</summary>
    Scoped
}
