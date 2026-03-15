// agent-notes: { ctx: "Document metadata from YAML front matter", deps: [IDocumentMetadata], state: active, last: "sato@2026-03-15" }

using Md2.Core.Slides;

namespace Md2.Core.Ast;

public class DocumentMetadata : IDocumentMetadata
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Date { get; set; }
    public string? Subject { get; set; }
    public string? Keywords { get; set; }
    public IReadOnlyDictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>();
}
