// agent-notes: { ctx: "AST transform that renders mermaid code blocks to PNG", deps: [IAstTransform, MermaidRenderer, MermaidThemeConfig, Markdig, Md2.Core.Ast], state: active, last: "sato@2026-03-13" }

using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Transforms;

namespace Md2.Diagrams;

/// <summary>
/// AST transform that finds mermaid FencedCodeBlocks, renders them
/// to PNG via MermaidRenderer, and annotates the block with the image path.
/// </summary>
public class MermaidDiagramRenderer : IAstTransform
{
    private readonly MermaidRenderer _renderer;

    public MermaidDiagramRenderer(MermaidRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public string Name => "MermaidDiagram";
    public int Order => 40;

    public MarkdownDocument Transform(MarkdownDocument doc, TransformContext context)
    {
        if (!context.Options.RenderMermaid)
            return doc;

        var themeConfig = context.ResolvedTheme != null
            ? MermaidThemeConfig.FromResolvedTheme(context.ResolvedTheme)
            : null;

        foreach (var block in doc.Descendants<FencedCodeBlock>())
        {
            if (!string.Equals(block.Info, "mermaid", StringComparison.OrdinalIgnoreCase))
                continue;

            var source = string.Join("\n", block.Lines);
            if (string.IsNullOrWhiteSpace(source))
                continue;

            try
            {
                var path = _renderer.RenderAsync(source, themeConfig, context.CancellationToken).GetAwaiter().GetResult();
                block.SetMermaidImagePath(path);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                context.AddWarning($"Mermaid rendering failed: {ex.Message}");
            }
        }

        return doc;
    }
}
