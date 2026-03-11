// agent-notes: { ctx: "Typed extension methods for Markdig AST data", deps: [Markdig, AstDataKeys, SyntaxToken, DocumentMetadata], state: "green", last: "sato@2026-03-11" }

using Markdig.Syntax;

namespace Md2.Core.Ast;

public static class AstExtensions
{
    public static void SetSyntaxTokens(this MarkdownObject node, IReadOnlyList<SyntaxToken> tokens)
    {
        node.SetData(AstDataKeys.SyntaxTokens, tokens);
    }

    public static IReadOnlyList<SyntaxToken>? GetSyntaxTokens(this MarkdownObject node)
    {
        return node.GetData(AstDataKeys.SyntaxTokens) as IReadOnlyList<SyntaxToken>;
    }

    public static void SetMermaidImagePath(this MarkdownObject node, string path)
    {
        node.SetData(AstDataKeys.MermaidImagePath, path);
    }

    public static string? GetMermaidImagePath(this MarkdownObject node)
    {
        return node.GetData(AstDataKeys.MermaidImagePath) as string;
    }

    public static void SetOmmlXml(this MarkdownObject node, string omml)
    {
        node.SetData(AstDataKeys.OmmlXml, omml);
    }

    public static string? GetOmmlXml(this MarkdownObject node)
    {
        return node.GetData(AstDataKeys.OmmlXml) as string;
    }

    public static void SetDocumentMetadata(this MarkdownObject node, DocumentMetadata metadata)
    {
        node.SetData(AstDataKeys.DocumentMetadata, metadata);
    }

    public static DocumentMetadata? GetDocumentMetadata(this MarkdownObject node)
    {
        return node.GetData(AstDataKeys.DocumentMetadata) as DocumentMetadata;
    }
}
