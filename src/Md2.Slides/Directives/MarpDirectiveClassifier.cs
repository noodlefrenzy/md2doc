// agent-notes: { ctx: "Classify MARP directives as global, local, or scoped", deps: [MarpDirective], state: active, last: "sato@2026-03-15" }

namespace Md2.Slides.Directives;

/// <summary>
/// Classifies MARP directives by scope according to Marpit v3.x semantics.
/// - Global: from front matter, applies to all slides
/// - Local: from HTML comments without underscore prefix, propagates forward
/// - Scoped: from HTML comments with underscore prefix (_key), applies to current slide only
/// </summary>
public static class MarpDirectiveClassifier
{
    /// <summary>
    /// Known MARP directive keys that can appear in local/scoped context.
    /// </summary>
    private static readonly HashSet<string> KnownDirectiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "theme", "paginate", "size", "headingDivider",
        "header", "footer", "class",
        "backgroundColor", "backgroundImage", "backgroundPosition",
        "backgroundRepeat", "backgroundSize", "color",
        "marp"
    };

    /// <summary>
    /// Classify a list of raw directives by applying scope rules.
    /// Directives already marked as Global (from front matter) are left as-is.
    /// HTML comment directives with underscore prefix become Scoped.
    /// HTML comment directives without prefix become Local.
    /// </summary>
    public static IReadOnlyList<MarpDirective> Classify(IReadOnlyList<MarpDirective> directives)
    {
        ArgumentNullException.ThrowIfNull(directives);

        var classified = new List<MarpDirective>(directives.Count);

        foreach (var directive in directives)
        {
            classified.Add(ClassifySingle(directive));
        }

        return classified;
    }

    /// <summary>
    /// Classify a single directive.
    /// </summary>
    public static MarpDirective ClassifySingle(MarpDirective directive)
    {
        ArgumentNullException.ThrowIfNull(directive);

        // Global directives (from front matter) stay global
        if (directive.Scope == MarpDirectiveScope.Global)
            return directive;

        // Check for underscore prefix → Scoped
        if (directive.Key.StartsWith('_'))
        {
            var normalizedKey = directive.Key[1..]; // strip underscore
            return directive with { Key = normalizedKey, Scope = MarpDirectiveScope.Scoped };
        }

        // No underscore → Local (propagates forward)
        return directive with { Scope = MarpDirectiveScope.Local };
    }

    /// <summary>
    /// Check whether a key is a known MARP directive.
    /// Unknown keys are still valid (passed through as custom fields).
    /// </summary>
    public static bool IsKnownDirective(string key)
    {
        // Strip underscore prefix for lookup
        var normalizedKey = key.StartsWith('_') ? key[1..] : key;
        return KnownDirectiveKeys.Contains(normalizedKey);
    }
}
