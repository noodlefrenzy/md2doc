// agent-notes: { ctx: "Core pipeline: parse, transform, emit with cancellation", deps: [Markdig, Md2.Parsing, IAstTransform, IFormatEmitter, ResolvedTheme, ILogger], state: active, last: "sato@2026-03-13" }

using Markdig;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Emit;
using Md2.Core.Transforms;
using Md2.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Md2.Core.Pipeline;

public class ConversionPipeline
{
    private readonly List<IAstTransform> _transforms = new();
    private readonly ILogger<ConversionPipeline> _logger;

    public ConversionPipeline()
        : this(NullLogger<ConversionPipeline>.Instance)
    {
    }

    public ConversionPipeline(ILogger<ConversionPipeline> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public MarkdownDocument Parse(string markdown, ParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(options);

        _logger.LogInformation("Parse starting ({Length} chars)", markdown.Length);
        var pipeline = Md2MarkdownPipeline.Build(options);
        var doc = Markdown.Parse(markdown, pipeline);
        _logger.LogInformation("Parse complete");
        return doc;
    }

    public void RegisterTransform(IAstTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        _transforms.Add(transform);
    }

    public TransformResult Transform(MarkdownDocument doc, TransformOptions options, CancellationToken cancellationToken = default, ResolvedTheme? resolvedTheme = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(options);

        var context = new TransformContext(new DocumentMetadata(), options, cancellationToken, resolvedTheme);

        foreach (var transform in _transforms.OrderBy(t => t.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Applying transform: {TransformName} (order {Order})", transform.Name, transform.Order);
            doc = transform.Transform(doc, context);
        }

        return new TransformResult(doc, context.Warnings);
    }

    public async Task Emit(MarkdownDocument doc, ResolvedTheme theme, IFormatEmitter emitter, EmitOptions options, Stream output)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(emitter);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        _logger.LogInformation("Emit starting (format: {Format})", emitter.FormatName);
        await emitter.EmitAsync(doc, theme, options, output);
        _logger.LogInformation("Emit complete");
    }
}
