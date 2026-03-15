// agent-notes: { ctx: "Per-slide MARP directives (bg, color, class, paginate, header, footer)", deps: [], state: active, last: "sato@2026-03-15" }

namespace Md2.Core.Slides;

/// <summary>
/// Per-slide MARP directives. These are set by the MARP parser from
/// HTML comment directives and cascaded across slides.
/// </summary>
public class SlideDirectives
{
    public string? BackgroundColor { get; set; }
    public string? BackgroundImage { get; set; }
    public string? Color { get; set; }
    public string? Class { get; set; }
    public bool? Paginate { get; set; }
    public string? Header { get; set; }
    public string? Footer { get; set; }
}
