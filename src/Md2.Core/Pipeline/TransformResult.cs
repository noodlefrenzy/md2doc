// agent-notes: { ctx: "Return type from ConversionPipeline.Transform with warnings", deps: [Markdig], state: active, last: "sato@2026-03-13" }

using Markdig.Syntax;

namespace Md2.Core.Pipeline;

/// <summary>
/// Result of the transform phase, containing the transformed document and any warnings.
/// </summary>
public class TransformResult
{
    public TransformResult(MarkdownDocument document, IReadOnlyList<string> warnings)
    {
        Document = document;
        Warnings = warnings.ToList().AsReadOnly();
    }

    public MarkdownDocument Document { get; }
    public IReadOnlyList<string> Warnings { get; }
}
