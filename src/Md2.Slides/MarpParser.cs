// agent-notes: { ctx: "Top-level MARP parser: string → SlideDocument", deps: [Markdig, Md2.Parsing, MarpDirectiveExtractor, MarpDirectiveClassifier, MarpSlideExtractor, SlideLayoutInferrer, MarpExtensionParser, YamlDotNet], state: active, last: "sato@2026-03-15" }

using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Md2.Core.Slides;
using Md2.Parsing;
using Md2.Slides.Directives;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Md2.Slides;

/// <summary>
/// Top-level MARP parser. Converts MARP-styled Markdown into a SlideDocument.
/// Orchestrates: front-matter extraction → Markdig parse → directive extraction →
/// classification → slide splitting → layout inference → assembly.
/// </summary>
public partial class MarpParser
{
    [GeneratedRegex(@"^---\s*\n(.*?)\n---\s*\n", RegexOptions.Singleline)]
    private static partial Regex FrontMatterRegex();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parse MARP-styled Markdown into a SlideDocument.
    /// </summary>
    public SlideDocument Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        // Step 1: Extract front matter before Markdig parsing
        var (frontMatter, bodyMarkdown) = ExtractFrontMatter(markdown);

        // Step 2: Extract global directives from front matter
        var globalDirectives = new List<MarpDirective>();
        var metadata = new PresentationMetadata();
        int? headingDivider = null;

        if (frontMatter != null)
        {
            globalDirectives.AddRange(
                MarpDirectiveClassifier.Classify(
                    MarpDirectiveExtractor.ExtractFromFrontMatter(frontMatter)));

            // Extract metadata
            if (frontMatter.TryGetValue("title", out var title))
                metadata.Title = title;
            if (frontMatter.TryGetValue("author", out var author))
                metadata.Author = author;
            if (frontMatter.TryGetValue("date", out var date))
                metadata.Date = date;
            if (frontMatter.TryGetValue("theme", out var theme))
                metadata.Theme = theme;
            if (frontMatter.TryGetValue("size", out var size))
                metadata.Size = ParseSize(size);
            if (frontMatter.TryGetValue("headingDivider", out var hdStr) && int.TryParse(hdStr, out var hd))
                headingDivider = hd;
        }

        // Step 3: Parse markdown through Markdig (without YAML front matter extension)
        var parserOptions = new ParserOptions { EnableYamlFrontMatter = false };
        var pipeline = Md2MarkdownPipeline.Build(parserOptions);
        var doc = Markdown.Parse(bodyMarkdown, pipeline);

        // Step 4: Extract and split into slides with directives
        var slideDoc = MarpSlideExtractor.Extract(doc, globalDirectives, headingDivider);
        slideDoc.Metadata = metadata;

        // Step 5: Infer layouts and apply md2 extensions
        foreach (var slide in slideDoc.Slides)
        {
            // Check for md2 extension comments in original content
            Md2Extension? ext = null;
            foreach (var block in slide.Content.OfType<HtmlBlock>())
            {
                var html = block.Lines.ToString().Trim();
                ext = MarpExtensionParser.TryParse(html);
                if (ext != null)
                    break;
            }

            // Infer layout
            slide.Layout = SlideLayoutInferrer.Infer(slide, ext);

            // Apply md2 extension properties
            if (ext != null)
            {
                if (ext.Build != null)
                    slide.Build = new BuildAnimation(Enum.Parse<BuildAnimationType>(ext.Build, ignoreCase: true));
                if (ext.Transition != null)
                    slide.Transition = new SlideTransition(ext.Transition)
                    {
                        DurationMs = ext.TransitionDurationMs
                    };
            }
        }

        return slideDoc;
    }

    /// <summary>
    /// Extract YAML front matter from the start of markdown text.
    /// Returns the parsed key-value pairs and the remaining markdown body.
    /// </summary>
    internal static (Dictionary<string, string>? FrontMatter, string Body) ExtractFrontMatter(string markdown)
    {
        var match = FrontMatterRegex().Match(markdown);
        if (!match.Success)
            return (null, markdown);

        var yamlContent = match.Groups[1].Value;
        var body = markdown[match.Length..];

        try
        {
            var dict = YamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            if (dict == null)
                return (null, markdown);

            // Flatten to string values
            var frontMatter = dict.ToDictionary(
                kv => kv.Key,
                kv => kv.Value?.ToString() ?? "");

            return (frontMatter, body);
        }
        catch
        {
            return (null, markdown);
        }
    }

    private static SlideSize ParseSize(string size)
    {
        return size.ToLowerInvariant() switch
        {
            "16:9" => SlideSize.Widescreen16x9,
            "4:3" => SlideSize.Standard4x3,
            "16:10" => SlideSize.Widescreen16x10,
            _ => SlideSize.Widescreen16x9
        };
    }
}
