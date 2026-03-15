// agent-notes: { ctx: "Top-level presentation IR: list of slides + metadata (ADR-0014)", deps: [Slide, PresentationMetadata, ResolvedTheme], state: active, last: "sato@2026-03-15" }

using Md2.Core.Pipeline;

namespace Md2.Core.Slides;

/// <summary>
/// The intermediate representation for a presentation.
/// Contains an ordered list of slides plus presentation-level metadata.
/// This is the contract between the MARP parser and the PPTX emitter.
/// </summary>
public class SlideDocument
{
    private readonly List<Slide> _slides = new();

    public PresentationMetadata Metadata { get; set; } = new();
    public IReadOnlyList<Slide> Slides => _slides;
    public ResolvedTheme? Theme { get; set; }

    public void AddSlide(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        _slides.Add(slide);
    }
}
