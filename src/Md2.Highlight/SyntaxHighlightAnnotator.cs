// agent-notes: { ctx: "AST transform that tokenizes fenced code blocks with syntax highlighting", deps: [IAstTransform, CodeTokenizer, Markdig, Md2.Core.Ast], state: active, last: "sato@2026-03-12" }

using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Transforms;

namespace Md2.Highlight;

public class SyntaxHighlightAnnotator : IAstTransform
{
    public string Name => "SyntaxHighlight";
    public int Order => 50;

    public MarkdownDocument Transform(MarkdownDocument doc, TransformContext context)
    {
        using var tokenizer = new CodeTokenizer();

        foreach (var block in doc.Descendants<FencedCodeBlock>())
        {
            var language = block.Info;
            if (string.IsNullOrWhiteSpace(language))
                continue;

            var code = string.Join("\n", block.Lines);
            if (string.IsNullOrEmpty(code))
                continue;

            var tokens = tokenizer.Tokenize(code, language);
            if (tokens.Count > 0)
            {
                block.SetSyntaxTokens(tokens);
            }
        }

        return doc;
    }
}
