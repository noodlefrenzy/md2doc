// agent-notes: { ctx: "Apply cascading semantics to MARP directives across slides", deps: [MarpDirective, SlideDirectives], state: active, last: "sato@2026-03-15" }

using Md2.Core.Slides;

namespace Md2.Slides.Directives;

/// <summary>
/// Applies Marpit v3.x cascading semantics to classified directives:
/// - Global: applied to all slides as a base layer
/// - Local: applied from the slide where declared, propagates forward until overridden
/// - Scoped: applied only to the slide where declared, does not propagate
/// </summary>
public static class MarpDirectiveCascader
{
    /// <summary>
    /// Cascade directives across the given number of slides.
    /// Returns one SlideDirectives per slide with fully resolved values.
    /// </summary>
    public static IReadOnlyList<SlideDirectives> Cascade(
        IReadOnlyList<MarpDirective> directives,
        int slideCount)
    {
        ArgumentNullException.ThrowIfNull(directives);
        if (slideCount < 0)
            throw new ArgumentOutOfRangeException(nameof(slideCount));

        if (slideCount == 0)
            return Array.Empty<SlideDirectives>();

        // Layer 1: Build base from global directives
        var globalBase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in directives.Where(d => d.Scope == MarpDirectiveScope.Global))
        {
            globalBase[d.Key] = d.Value;
        }

        // Layer 2: Group local and scoped directives by slide index
        var localBySlide = directives
            .Where(d => d.Scope == MarpDirectiveScope.Local && d.SlideIndex >= 0)
            .GroupBy(d => d.SlideIndex)
            .ToDictionary(g => g.Key, g => g.ToList());

        var scopedBySlide = directives
            .Where(d => d.Scope == MarpDirectiveScope.Scoped && d.SlideIndex >= 0)
            .GroupBy(d => d.SlideIndex)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Layer 3: Cascade forward
        var results = new SlideDirectives[slideCount];
        var runningState = new Dictionary<string, string>(globalBase, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < slideCount; i++)
        {
            // Apply local directives at this slide (they propagate forward)
            if (localBySlide.TryGetValue(i, out var locals))
            {
                foreach (var d in locals)
                {
                    runningState[d.Key] = d.Value;
                }
            }

            // Start with running state (global + accumulated locals)
            var slideState = new Dictionary<string, string>(runningState, StringComparer.OrdinalIgnoreCase);

            // Apply scoped directives (current slide only, don't affect running state)
            if (scopedBySlide.TryGetValue(i, out var scoped))
            {
                foreach (var d in scoped)
                {
                    slideState[d.Key] = d.Value;
                }
            }

            results[i] = BuildSlideDirectives(slideState);
        }

        return results;
    }

    private static SlideDirectives BuildSlideDirectives(Dictionary<string, string> state)
    {
        var directives = new SlideDirectives();

        if (state.TryGetValue("backgroundColor", out var bgColor))
            directives.BackgroundColor = bgColor;

        if (state.TryGetValue("backgroundImage", out var bgImage))
            directives.BackgroundImage = bgImage;

        if (state.TryGetValue("color", out var color))
            directives.Color = color;

        if (state.TryGetValue("class", out var cls))
            directives.Class = cls;

        if (state.TryGetValue("paginate", out var paginate))
            directives.Paginate = string.Equals(paginate, "true", StringComparison.OrdinalIgnoreCase);

        if (state.TryGetValue("header", out var header))
            directives.Header = header;

        if (state.TryGetValue("footer", out var footer))
            directives.Footer = footer;

        return directives;
    }
}
