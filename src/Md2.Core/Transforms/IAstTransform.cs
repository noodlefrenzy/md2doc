// agent-notes: { ctx: "Interface for ordered AST transforms", deps: [Markdig, TransformContext], state: "green", last: "sato@2026-03-11" }

using Markdig.Syntax;

namespace Md2.Core.Transforms;

public interface IAstTransform
{
    string Name { get; }
    int Order { get; }
    MarkdownDocument Transform(MarkdownDocument doc, TransformContext context);
}
