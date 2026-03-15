// agent-notes: { ctx: "Split Markdig AST at slide boundaries, extract per-slide directives + notes", deps: [Markdig, MarpDirectiveExtractor, MarpDirectiveClassifier, MarpDirectiveCascader, Slide, SlideDocument], state: active, last: "sato@2026-03-15" }

using Markdig.Syntax;
using Md2.Core.Slides;
using Md2.Slides.Directives;

namespace Md2.Slides;

/// <summary>
/// Splits a Markdig AST at slide boundaries (ThematicBreakBlock and headingDivider)
/// and produces per-slide content fragments with directives and speaker notes.
/// </summary>
public static class MarpSlideExtractor
{
    /// <summary>
    /// Extract slides from a parsed MarkdownDocument.
    /// Splits at ThematicBreakBlock nodes (--- separators).
    /// If headingDivider is set, also splits before headings of that level or higher.
    /// </summary>
    public static SlideDocument Extract(
        MarkdownDocument doc,
        IReadOnlyList<MarpDirective> classifiedDirectives,
        int? headingDivider = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(classifiedDirectives);

        var slideDoc = new SlideDocument();
        var allBlocks = doc.ToList(); // snapshot to avoid mutation issues
        var currentBlocks = new List<Block>();
        var currentSpeakerNotes = new List<string>();
        var slideIndex = 0;

        // Track which directives belong to which slide
        var directivesWithSlideIndex = new List<MarpDirective>();
        var htmlBlockSlideMap = BuildHtmlBlockSlideMap(allBlocks, headingDivider);

        // Assign slide indices to classified directives that came from HTML blocks
        foreach (var directive in classifiedDirectives)
        {
            if (directive.Scope == MarpDirectiveScope.Global)
            {
                directivesWithSlideIndex.Add(directive);
            }
        }

        foreach (var block in allBlocks)
        {
            var isSlideBreak = block is ThematicBreakBlock;
            var isHeadingBreak = headingDivider.HasValue
                && block is HeadingBlock heading
                && heading.Level <= headingDivider.Value
                && currentBlocks.Count > 0;

            if (isSlideBreak || isHeadingBreak)
            {
                if (currentBlocks.Count > 0 || isSlideBreak)
                {
                    var slide = CreateSlide(slideIndex, currentBlocks, currentSpeakerNotes, doc);
                    slideDoc.AddSlide(slide);
                    slideIndex++;
                    currentBlocks = new List<Block>();
                    currentSpeakerNotes = new List<string>();
                }

                if (isSlideBreak)
                    continue; // skip the break itself

                // For heading breaks, the heading goes into the new slide
            }

            // Check if this is an HTML block with directives, speaker notes, or md2 extensions
            if (block is HtmlBlock htmlBlock)
            {
                var html = htmlBlock.Lines.ToString().Trim();

                // md2 extensions are kept in content (MarpParser reads them from there)
                if (MarpExtensionParser.IsMd2Extension(html))
                {
                    currentBlocks.Add(block);
                    continue;
                }

                // Check for speaker notes
                var note = MarpDirectiveExtractor.ExtractSpeakerNote(html);
                if (note != null)
                {
                    currentSpeakerNotes.Add(note);
                    continue; // don't add speaker note to slide content
                }

                // Extract inline directives and assign slide index
                var inlineDirectives = ExtractInlineDirectives(html, slideIndex);
                directivesWithSlideIndex.AddRange(inlineDirectives);

                // Skip directive-only HTML blocks from content
                if (inlineDirectives.Count > 0)
                    continue;
            }

            currentBlocks.Add(block);
        }

        // Final slide
        if (currentBlocks.Count > 0)
        {
            var slide = CreateSlide(slideIndex, currentBlocks, currentSpeakerNotes, doc);
            slideDoc.AddSlide(slide);
        }

        // Cascade directives across all slides
        var cascaded = MarpDirectiveCascader.Cascade(directivesWithSlideIndex, slideDoc.Slides.Count);
        for (var i = 0; i < cascaded.Count; i++)
        {
            slideDoc.Slides[i].Directives = cascaded[i];
        }

        return slideDoc;
    }

    private static List<MarpDirective> ExtractInlineDirectives(string html, int slideIndex)
    {
        if (string.IsNullOrWhiteSpace(html) || !html.StartsWith("<!--") || !html.EndsWith("-->"))
            return new List<MarpDirective>();

        // Parse as a single-block doc to reuse extractor
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var miniDoc = Markdig.Markdown.Parse(html, pipeline);
        var raw = MarpDirectiveExtractor.Extract(miniDoc);

        // Classify and assign slide index
        var classified = MarpDirectiveClassifier.Classify(raw);
        return classified.Select(d => d with { SlideIndex = slideIndex }).ToList();
    }

    private static Slide CreateSlide(int index, List<Block> blocks, List<string> speakerNotes, MarkdownDocument parent)
    {
        var fragment = new MarkdownDocument();
        foreach (var block in blocks)
        {
            parent.Remove(block);
            fragment.Add(block);
        }

        var slide = new Slide(index, fragment);
        if (speakerNotes.Count > 0)
        {
            slide.SpeakerNotes = string.Join("\n\n", speakerNotes);
        }
        return slide;
    }

    private static Dictionary<Block, int> BuildHtmlBlockSlideMap(List<Block> blocks, int? headingDivider)
    {
        var map = new Dictionary<Block, int>();
        var slideIndex = 0;

        foreach (var block in blocks)
        {
            if (block is ThematicBreakBlock)
            {
                slideIndex++;
                continue;
            }
            if (headingDivider.HasValue && block is HeadingBlock h && h.Level <= headingDivider.Value && map.Count > 0)
            {
                slideIndex++;
            }
            map[block] = slideIndex;
        }

        return map;
    }
}
