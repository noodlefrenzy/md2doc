// agent-notes: { ctx: "Shared metadata interface for document and presentation types", deps: [], state: active, last: "sato@2026-03-15" }

namespace Md2.Core.Slides;

/// <summary>
/// Shared metadata interface implemented by both DocumentMetadata (DOCX)
/// and PresentationMetadata (PPTX) to prevent drift.
/// </summary>
public interface IDocumentMetadata
{
    string? Title { get; set; }
    string? Author { get; set; }
    string? Date { get; set; }
    IReadOnlyDictionary<string, string> CustomFields { get; set; }
}
