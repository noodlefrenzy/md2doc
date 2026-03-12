// agent-notes: { ctx: "AST transform that converts LaTeX math to OMML annotations", deps: [IAstTransform, LatexToOmmlConverter, Markdig.Extensions.Mathematics, Md2.Core.Ast], state: active, last: "sato@2026-03-12" }

using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Core.Ast;
using Md2.Core.Transforms;

namespace Md2.Math;

/// <summary>
/// AST transform that finds MathBlock and MathInline nodes,
/// converts their LaTeX content to OMML via LatexToOmmlConverter,
/// and attaches the OMML XML to the AST via SetOmmlXml.
/// </summary>
public class MathBlockAnnotator : IAstTransform
{
    private readonly LatexToOmmlConverter _converter;

    public MathBlockAnnotator(LatexToOmmlConverter converter)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public string Name => "MathBlockAnnotator";
    public int Order => 35;

    public MarkdownDocument Transform(MarkdownDocument doc, TransformContext context)
    {
        // Process display math blocks ($$\n...\n$$)
        foreach (var block in doc.Descendants<MathBlock>())
        {
            var latex = string.Join("\n", block.Lines);
            if (string.IsNullOrWhiteSpace(latex))
                continue;

            try
            {
                var omml = _converter.ConvertAsync(latex, context.CancellationToken).GetAwaiter().GetResult();
                block.SetOmmlXml(omml);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                context.AddWarning($"Math rendering failed for display math: {ex.Message}");
            }
        }

        // Process inline math ($...$ and $$...$$)
        foreach (var inline in doc.Descendants<MathInline>())
        {
            var latex = inline.Content.ToString();
            if (string.IsNullOrWhiteSpace(latex))
                continue;

            try
            {
                var omml = _converter.ConvertAsync(latex, context.CancellationToken).GetAwaiter().GetResult();
                inline.SetOmmlXml(omml);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                context.AddWarning($"Math rendering failed for inline math: {ex.Message}");
            }
        }

        return doc;
    }
}
