// agent-notes: { ctx: "Core pipeline: parse, transform, emit", deps: [Markdig, Md2.Parsing, IAstTransform, IFormatEmitter], state: "green", last: "sato@2026-03-11" }

using Markdig;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Emit;
using Md2.Core.Transforms;
using Md2.Parsing;

namespace Md2.Core.Pipeline;

public class ConversionPipeline
{
    private readonly List<IAstTransform> _transforms = new();

    public MarkdownDocument Parse(string markdown, ParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(options);

        var pipeline = Md2MarkdownPipeline.Build(options);
        return Markdown.Parse(markdown, pipeline);
    }

    public void RegisterTransform(IAstTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        _transforms.Add(transform);
    }

    public MarkdownDocument Transform(MarkdownDocument doc, TransformOptions options)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(options);

        var context = new TransformContext(new DocumentMetadata(), options);

        foreach (var transform in _transforms.OrderBy(t => t.Order))
        {
            doc = transform.Transform(doc, context);
        }

        return doc;
    }

    public async Task Emit(MarkdownDocument doc, ResolvedTheme theme, IFormatEmitter emitter, EmitOptions options, Stream output)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(emitter);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        await emitter.EmitAsync(doc, theme, options, output);
    }
}
