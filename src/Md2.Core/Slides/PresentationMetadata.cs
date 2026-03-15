// agent-notes: { ctx: "Presentation metadata extending shared IDocumentMetadata", deps: [IDocumentMetadata, SlideSize], state: active, last: "sato@2026-03-15" }

namespace Md2.Core.Slides;

/// <summary>
/// Metadata for a presentation. Implements IDocumentMetadata for shared
/// properties with DocumentMetadata (DOCX). Adds presentation-specific
/// fields like theme and slide size.
/// </summary>
public class PresentationMetadata : IDocumentMetadata
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Date { get; set; }
    public IReadOnlyDictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>();

    // Presentation-specific
    public string? Theme { get; set; }
    public SlideSize Size { get; set; } = SlideSize.Widescreen16x9;
}
