// agent-notes: { ctx: "AST transform that extracts front matter", deps: [FrontMatterExtractor, IAstTransform, AstExtensions], state: "green", last: "sato@2026-03-11" }

using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Parsing;

namespace Md2.Core.Transforms;

public class YamlFrontMatterExtractor : IAstTransform
{
    public string Name => "YamlFrontMatterExtractor";
    public int Order => 10;

    public MarkdownDocument Transform(MarkdownDocument doc, TransformContext context)
    {
        var metadata = FrontMatterExtractor.Extract(doc);

        // Copy extracted metadata into context
        context.Metadata.Title = metadata.Title;
        context.Metadata.Author = metadata.Author;
        context.Metadata.Date = metadata.Date;
        context.Metadata.Subject = metadata.Subject;
        context.Metadata.Keywords = metadata.Keywords;
        context.Metadata.CustomFields = metadata.CustomFields;

        // Store metadata on the AST
        doc.SetDocumentMetadata(metadata);

        // Remove front matter blocks from the document
        var frontMatterBlocks = doc.OfType<YamlFrontMatterBlock>().ToList();
        foreach (var block in frontMatterBlocks)
        {
            doc.Remove(block);
        }

        return doc;
    }
}
