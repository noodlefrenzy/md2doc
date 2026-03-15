// agent-notes: { ctx: "Slide dimensions in EMUs (English Metric Units)", deps: [], state: active, last: "sato@2026-03-15" }

namespace Md2.Core.Slides;

/// <summary>
/// Slide dimensions in EMUs (English Metric Units).
/// 1 inch = 914400 EMUs. Standard PPTX sizes.
/// </summary>
public record SlideSize(long Width, long Height)
{
    /// <summary>16:9 widescreen (13.333" x 7.5")</summary>
    public static readonly SlideSize Widescreen16x9 = new(12192000, 6858000);

    /// <summary>4:3 standard (10" x 7.5")</summary>
    public static readonly SlideSize Standard4x3 = new(9144000, 6858000);

    /// <summary>16:10 widescreen (12" x 7.5")</summary>
    public static readonly SlideSize Widescreen16x10 = new(10972800, 6858000);
}
