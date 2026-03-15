// agent-notes: { ctx: "Infer slide layout from content structure + class directive", deps: [Markdig, SlideLayout, Slide], state: active, last: "sato@2026-03-15" }

using Markdig.Syntax;
using Md2.Core.Slides;

namespace Md2.Slides;

/// <summary>
/// Infers the SlideLayout for a slide based on its content structure
/// and any class directive applied to it.
/// </summary>
public static class SlideLayoutInferrer
{
    private static readonly Dictionary<string, SlideLayout> ClassToLayout = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lead"] = SlideLayout.Title,
        ["title"] = SlideLayout.Title,
        ["invert"] = SlideLayout.Content, // invert is a style, not a layout
        ["two-column"] = SlideLayout.TwoColumn,
        ["split"] = SlideLayout.TwoColumn,
    };

    /// <summary>
    /// Infer the layout for a slide based on its content and directives.
    /// Priority: md2 extension layout > class directive > content heuristics.
    /// </summary>
    public static SlideLayout Infer(Slide slide, Md2Extension? md2Extension = null)
    {
        ArgumentNullException.ThrowIfNull(slide);

        // Priority 1: Explicit md2 extension layout
        if (md2Extension?.Layout != null)
        {
            return ResolveLayoutName(md2Extension.Layout);
        }

        // Priority 2: Class directive mapping
        var classDirective = slide.Directives.Class;
        if (!string.IsNullOrEmpty(classDirective))
        {
            if (ClassToLayout.TryGetValue(classDirective, out var classLayout))
                return classLayout;

            // Unknown class — return as custom layout
            return new SlideLayout(classDirective);
        }

        // Priority 3: Content heuristics
        return InferFromContent(slide.Content);
    }

    /// <summary>
    /// Resolve a layout name string to a SlideLayout.
    /// </summary>
    public static SlideLayout ResolveLayoutName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return SlideLayout.Content;

        return name.ToLowerInvariant() switch
        {
            "content" => SlideLayout.Content,
            "title" => SlideLayout.Title,
            "two-column" => SlideLayout.TwoColumn,
            "section-divider" or "section" => SlideLayout.SectionDivider,
            "blank" => SlideLayout.Blank,
            _ => new SlideLayout(name)
        };
    }

    private static SlideLayout InferFromContent(MarkdownDocument content)
    {
        var blocks = content.ToList();

        // Empty slide → blank
        if (blocks.Count == 0)
            return SlideLayout.Blank;

        // Single heading with no other content → title slide
        if (blocks.Count == 1 && blocks[0] is HeadingBlock { Level: 1 })
            return SlideLayout.Title;

        // Heading followed by short paragraph → title slide
        if (blocks.Count == 2
            && blocks[0] is HeadingBlock { Level: 1 }
            && blocks[1] is ParagraphBlock para)
        {
            // Use Span to get the full text range of the paragraph
            var span = para.Span;
            if (span.Length < 100)
                return SlideLayout.Title;
        }

        // Only a thematic break or nothing meaningful
        if (blocks.All(b => b is ThematicBreakBlock))
            return SlideLayout.Blank;

        return SlideLayout.Content;
    }
}
