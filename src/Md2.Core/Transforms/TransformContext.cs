// agent-notes: { ctx: "Context passed to AST transforms", deps: [DocumentMetadata, TransformOptions, ResolvedTheme], state: "green", last: "sato@2026-03-13" }

using Md2.Core.Ast;
using Md2.Core.Pipeline;

namespace Md2.Core.Transforms;

public class TransformContext
{
    public TransformContext(DocumentMetadata metadata, TransformOptions options, CancellationToken cancellationToken = default, ResolvedTheme? resolvedTheme = null)
    {
        Metadata = metadata;
        Options = options;
        CancellationToken = cancellationToken;
        ResolvedTheme = resolvedTheme;
    }

    public DocumentMetadata Metadata { get; }
    public TransformOptions Options { get; }
    public CancellationToken CancellationToken { get; }
    public ResolvedTheme? ResolvedTheme { get; }
    public List<string> Warnings { get; } = new();

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }
}
