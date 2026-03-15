// agent-notes: { ctx: "Open record for slide layout types (not a closed enum per Wei debate)", deps: [], state: active, last: "sato@2026-03-15" }

namespace Md2.Core.Slides;

/// <summary>
/// Represents a slide layout. Uses an open record (not a closed enum) so that
/// MARP class directives like "lead" or "invert" can map to custom layouts.
/// The emitter maps known names to PPTX slide masters; unknown names fall back
/// to Content with a warning.
/// </summary>
public record SlideLayout(string Name)
{
    public static readonly SlideLayout Content = new("content");
    public static readonly SlideLayout Title = new("title");
    public static readonly SlideLayout TwoColumn = new("two-column");
    public static readonly SlideLayout SectionDivider = new("section-divider");
    public static readonly SlideLayout Blank = new("blank");
}
