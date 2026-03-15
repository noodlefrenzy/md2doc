// agent-notes: { ctx: "Pipeline orchestrator for PPTX: parse → transform → build slides → emit", deps: [Markdig, Md2.Parsing, IAstTransform, ISlideEmitter, SlideDocument], state: active, last: "sato@2026-03-15" }

using Markdig;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Emit;
using Md2.Core.Slides;
using Md2.Core.Transforms;
using Md2.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Md2.Core.Pipeline;

/// <summary>
/// Pipeline orchestrator for slide-based output (PPTX).
/// Parallel to ConversionPipeline but produces SlideDocument instead of emitting directly from AST.
/// Flow: Parse → Transform (full doc) → BuildSlideDocument (split at breaks) → Emit.
/// </summary>
public class SlidePipeline
{
    private readonly List<IAstTransform> _transforms = new();
    private readonly ILogger<SlidePipeline> _logger;

    public SlidePipeline()
        : this(NullLogger<SlidePipeline>.Instance)
    {
    }

    public SlidePipeline(ILogger<SlidePipeline> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public MarkdownDocument Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        _logger.LogInformation("SlidePipeline parse starting ({Length} chars)", markdown.Length);

        // Disable YAML front matter extension so --- is parsed as ThematicBreakBlock
        // (MARP front matter is handled separately by MarpParser)
        var options = new ParserOptions { EnableYamlFrontMatter = false };
        var pipeline = Md2MarkdownPipeline.Build(options);
        var doc = Markdown.Parse(markdown, pipeline);

        _logger.LogInformation("SlidePipeline parse complete");
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

    /// <summary>
    /// Split a transformed MarkdownDocument into a SlideDocument by splitting at ThematicBreakBlock boundaries.
    /// Annotations set by transforms (SetData) are preserved on the reparented nodes.
    /// </summary>
    public SlideDocument BuildSlideDocument(MarkdownDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        _logger.LogInformation("Building SlideDocument from parsed AST");

        var slideDoc = new SlideDocument();

        // Snapshot blocks to avoid mutation during iteration
        var allBlocks = doc.ToList();
        var currentBlocks = new List<Block>();
        var slideIndex = 0;

        foreach (var block in allBlocks)
        {
            if (block is ThematicBreakBlock)
            {
                if (currentBlocks.Count > 0)
                {
                    slideDoc.AddSlide(CreateSlide(slideIndex++, currentBlocks, doc));
                    currentBlocks = new List<Block>();
                }
            }
            else
            {
                currentBlocks.Add(block);
            }
        }

        // Final slide (or only slide if no breaks)
        if (currentBlocks.Count > 0)
        {
            slideDoc.AddSlide(CreateSlide(slideIndex, currentBlocks, doc));
        }

        _logger.LogInformation("Built SlideDocument with {SlideCount} slides", slideDoc.Slides.Count);
        return slideDoc;
    }

    public async Task Emit(SlideDocument doc, ResolvedTheme theme, ISlideEmitter emitter, EmitOptions options, Stream output)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(emitter);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        _logger.LogInformation("SlidePipeline emit starting (format: {Format})", emitter.FormatName);
        await emitter.EmitAsync(doc, theme, options, output);
        _logger.LogInformation("SlidePipeline emit complete");
    }

    private static Slide CreateSlide(int index, List<Block> blocks, MarkdownDocument parent)
    {
        var fragment = new MarkdownDocument();
        foreach (var block in blocks)
        {
            parent.Remove(block);
            fragment.Add(block);
        }
        return new Slide(index, fragment);
    }
}
