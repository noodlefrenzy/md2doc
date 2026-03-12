// agent-notes: { ctx: "Context passed to AST transforms", deps: [DocumentMetadata, TransformOptions], state: "green", last: "sato@2026-03-12" }

using Md2.Core.Ast;
using Md2.Core.Pipeline;

namespace Md2.Core.Transforms;

public class TransformContext
{
    public TransformContext(DocumentMetadata metadata, TransformOptions options, CancellationToken cancellationToken = default)
    {
        Metadata = metadata;
        Options = options;
        CancellationToken = cancellationToken;
    }

    public DocumentMetadata Metadata { get; }
    public TransformOptions Options { get; }
    public CancellationToken CancellationToken { get; }
    public List<string> Warnings { get; } = new();

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }
}
