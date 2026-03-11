// agent-notes: { ctx: "Custom Markdig extension for admonition blocks", deps: [Markdig], state: "green", last: "sato@2026-03-11" }

using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;

namespace Md2.Parsing;

/// <summary>
/// A container block representing an admonition (e.g., !!! note "Title").
/// </summary>
public class AdmonitionBlock : ContainerBlock
{
    public AdmonitionBlock(BlockParser? parser) : base(parser)
    {
    }

    public string AdmonitionType { get; set; } = string.Empty;
    public string? Title { get; set; }
}

/// <summary>
/// Parses admonition blocks using the !!! syntax.
/// </summary>
public class AdmonitionBlockParser : BlockParser
{
    private const string OpeningMarker = "!!!";
    private const int IndentWidth = 4;

    public AdmonitionBlockParser()
    {
        OpeningCharacters = ['!'];
    }

    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (processor.IsCodeIndent)
            return BlockState.None;

        var line = processor.Line;
        var startPosition = line.Start;

        // Check for !!! prefix
        if (!line.Match(OpeningMarker))
            return BlockState.None;

        // Advance past !!!
        line.Start += 3;

        // Must have a space after !!!
        if (line.CurrentChar != ' ' && !line.IsEmpty)
            return BlockState.None;

        line.TrimStart();

        // Extract the admonition type (first word)
        var typeStart = line.Start;
        while (!line.IsEmpty && line.CurrentChar != ' ' && line.CurrentChar != '"')
        {
            line.NextChar();
        }

        var admonitionType = line.Text?.Substring(typeStart, line.Start - typeStart)?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(admonitionType))
            return BlockState.None;

        // Extract optional quoted title
        line.TrimStart();
        string? title = null;
        if (line.CurrentChar == '"')
        {
            line.NextChar(); // skip opening quote
            var titleStart = line.Start;
            while (!line.IsEmpty && line.CurrentChar != '"')
            {
                line.NextChar();
            }
            title = line.Text?.Substring(titleStart, line.Start - titleStart);
            if (line.CurrentChar == '"')
                line.NextChar(); // skip closing quote
        }

        var block = new AdmonitionBlock(this)
        {
            AdmonitionType = admonitionType,
            Title = title,
            Span = new SourceSpan(startPosition, line.End),
            Line = processor.LineIndex,
            Column = processor.Column
        };

        processor.NewBlocks.Push(block);
        return BlockState.ContinueDiscard;
    }

    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        if (processor.IsBlankLine)
        {
            return BlockState.ContinueDiscard;
        }

        // Content must be indented by at least 4 spaces
        if (processor.Indent >= IndentWidth)
        {
            processor.GoToColumn(processor.ColumnBeforeIndent + IndentWidth);
            return BlockState.Continue;
        }

        // Check if this is a nested admonition (indented !!! within an admonition)
        var line = processor.Line;
        var saved = line.Start;
        line.TrimStart();
        if (line.Match(OpeningMarker))
        {
            line.Start = saved;
            // Not indented enough for this block, so close it
        }

        return BlockState.None;
    }
}

/// <summary>
/// Markdig extension that registers the admonition block parser.
/// </summary>
public class AdmonitionExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.BlockParsers.Contains<AdmonitionBlockParser>())
        {
            pipeline.BlockParsers.Insert(0, new AdmonitionBlockParser());
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}
