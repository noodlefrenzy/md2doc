// agent-notes: { ctx: "shared helper: extract plain text from Markdig inline AST", deps: [], state: active, last: "sato@2026-03-12" }

using Markdig.Syntax.Inlines;

namespace Md2.Emit.Docx;

/// <summary>
/// Extracts plain text content from Markdig inline AST nodes.
/// Shared across TableBuilder, ListBuilder, and DocxAstVisitor to avoid duplication (TD-004).
/// </summary>
internal static class InlineTextExtractor
{
    /// <summary>
    /// Recursively extracts plain text from an inline node.
    /// </summary>
    public static string Extract(Inline inline)
    {
        return inline switch
        {
            LiteralInline literal => literal.Content.ToString(),
            EmphasisInline emphasis => string.Join("", emphasis.Select(Extract)),
            CodeInline code => code.Content,
            LinkInline link => string.Join("", link.Select(Extract)),
            ContainerInline container => string.Join("", container.Select(Extract)),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Extracts plain text from a container inline (e.g., a paragraph's inline content).
    /// </summary>
    public static string Extract(ContainerInline container)
    {
        return string.Join("", container.Select(Extract));
    }
}
