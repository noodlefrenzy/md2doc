// agent-notes: { ctx: "Build animation settings for per-slide bullet reveal", deps: [], state: active, last: "sato@2026-03-15" }

namespace Md2.Core.Slides;

public record BuildAnimation(BuildAnimationType Type);

public enum BuildAnimationType
{
    Bullets
}
