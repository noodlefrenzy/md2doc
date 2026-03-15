// agent-notes: { ctx: "Slide transition settings", deps: [], state: active, last: "sato@2026-03-15" }

namespace Md2.Core.Slides;

public record SlideTransition(string Type)
{
    public int? DurationMs { get; init; }
}
