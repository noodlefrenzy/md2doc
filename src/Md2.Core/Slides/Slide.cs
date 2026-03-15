// agent-notes: { ctx: "Single slide in a SlideDocument, contains Markdig AST fragment + metadata", deps: [SlideLayout, SlideDirectives, BuildAnimation, SlideTransition, Markdig], state: active, last: "sato@2026-03-15" }

using Markdig.Syntax;

namespace Md2.Core.Slides;

/// <summary>
/// A single slide containing a Markdig AST fragment for content
/// plus typed metadata for layout, directives, speaker notes,
/// build animations, and transitions.
/// </summary>
public class Slide
{
    public Slide(int index, MarkdownDocument content)
    {
        Index = index;
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public int Index { get; }
    public MarkdownDocument Content { get; }
    public SlideLayout Layout { get; set; } = SlideLayout.Content;
    public string? SpeakerNotes { get; set; }
    public SlideDirectives Directives { get; set; } = new();
    public BuildAnimation? Build { get; set; }
    public SlideTransition? Transition { get; set; }
}
