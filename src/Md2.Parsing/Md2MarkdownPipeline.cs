// agent-notes: { ctx: "Builds configured Markdig pipeline from options", deps: [Markdig, ParserOptions, AdmonitionExtension], state: "green", last: "sato@2026-03-11" }

using Markdig;

namespace Md2.Parsing;

public static class Md2MarkdownPipeline
{
    public static MarkdownPipeline Build(ParserOptions? options = null)
    {
        options ??= new ParserOptions();

        var builder = new MarkdownPipelineBuilder()
            .EnableTrackTrivia();

        if (options.EnableGfm)
        {
            builder.UsePipeTables();
            builder.UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough);
            builder.UseTaskLists();
            builder.UseAutoLinks();
        }

        if (options.EnableMath)
        {
            builder.UseMathematics();
        }

        if (options.EnableAdmonitions)
        {
            builder.Use<AdmonitionExtension>();
        }

        if (options.EnableDefinitionLists)
        {
            builder.UseDefinitionLists();
        }

        if (options.EnableAttributes)
        {
            builder.UseGenericAttributes();
        }

        if (options.EnableYamlFrontMatter)
        {
            builder.UseYamlFrontMatter();
        }

        builder.UseFootnotes();

        return builder.Build();
    }
}
