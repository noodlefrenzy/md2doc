---
agent-notes: { ctx: "ADR for SlideDocument IR separating slide model from Markdig AST", deps: [docs/adrs/0005-native-markdig-ast.md, docs/code-map.md], state: proposed, last: "archie@2026-03-15", key: ["Wei debate complete — 3 changes required before acceptance"] }
---

# ADR-0014: SlideDocument Intermediate Representation

## Status

Proposed

## Context

ADR-0005 chose Markdig's native `MarkdownDocument` AST as the document representation for the DOCX pipeline, and noted: "If PPTX requires a fundamentally different document structure in v2, introduce a targeted `SlideDocument` model at that point."

That point has arrived. PPTX output requires structurally different representation from DOCX:

- **Slides** are the top-level unit, not a continuous document flow.
- **Slide layouts** (title, content, two-column, section divider, blank) dictate element placement.
- **Speaker notes** are per-slide metadata, not inline content.
- **Build animations** are per-element properties that have no document equivalent.
- **Background images/colors** are per-slide, not per-element.
- **Charts and native shapes** are slide-specific element types with no DOCX analogue.

The Markdig AST can represent the *content* of a slide (headings, paragraphs, lists, code blocks), but it cannot represent the *structure* of a presentation (slide boundaries, layouts, per-slide directives, speaker notes, build order).

**Options evaluated:**

1. **Annotate the Markdig AST with slide metadata** (Concept A from discovery). Slide boundaries are markers in the linear AST. Layouts, notes, and animations are attached via `SetData()`. The PPTX emitter walks the annotated Markdig AST.

2. **Dedicated MARP parser producing a SlideDocument IR** (Concept B — chosen). A new `MarpParser` produces a `SlideDocument` that is a list of `Slide` objects, each containing Markdig AST fragments for content plus typed metadata for layout, notes, directives, and animations.

3. **Markdig parse + lowering pass to SlideDocument** (Concept C from discovery). Markdig parses the full document, then a lowering pass splits it into a `SlideDocument`. This reuses Markdig parsing but adds an explicit conversion step.

## Decision

Introduce a **`SlideDocument` intermediate representation** in `Md2.Core` that models presentations as a list of slides. Each slide contains:

- A Markdig AST fragment (the slide's content — reuses the existing AST types)
- Typed slide metadata (layout, directives, speaker notes, build settings, background)

The `SlideDocument` lives in `Md2.Core` (not in the parser or emitter) because it is the contract between the MARP parser and the PPTX emitter — and potentially future slide-targeting parsers or emitters.

```csharp
// Md2.Core/Slides/SlideDocument.cs
public class SlideDocument
{
    public PresentationMetadata Metadata { get; set; }  // title, author, theme, size
    public IReadOnlyList<Slide> Slides { get; }
    public ResolvedTheme? Theme { get; set; }
}

// Md2.Core/Slides/Slide.cs
public class Slide
{
    public int Index { get; }
    public SlideLayout Layout { get; set; }          // Title, Content, TwoColumn, SectionDivider, Blank
    public MarkdownDocument Content { get; }          // Markdig AST fragment for this slide
    public string? SpeakerNotes { get; set; }
    public SlideDirectives Directives { get; set; }   // bg, color, class, paginate, header, footer
    public BuildAnimation? Build { get; set; }        // bullet-by-bullet reveal settings
    public SlideTransition? Transition { get; set; }  // transition between this and next slide
}

// Md2.Core/Slides/SlideDirectives.cs
public class SlideDirectives
{
    public string? BackgroundColor { get; set; }
    public string? BackgroundImage { get; set; }
    public string? Color { get; set; }
    public string? Class { get; set; }
    public bool? Paginate { get; set; }
    public string? Header { get; set; }
    public string? Footer { get; set; }
}

// Md2.Core/Slides/SlideLayout.cs  (Wei debate: replaced enum with open record)
public record SlideLayout(string Name)
{
    public static readonly SlideLayout Content = new("content");
    public static readonly SlideLayout Title = new("title");
    public static readonly SlideLayout TwoColumn = new("two-column");
    public static readonly SlideLayout SectionDivider = new("section-divider");
    public static readonly SlideLayout Blank = new("blank");
    // MARP class directives (e.g. "lead", "invert") map to custom layouts.
    // The emitter maps known names to PPTX slide masters; unknown → Content with warning.
}

// Md2.Core/Slides/PresentationMetadata.cs  (Wei debate: extends shared IDocumentMetadata)
public class PresentationMetadata : IDocumentMetadata
{
    // Shared with DocumentMetadata via IDocumentMetadata
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Date { get; set; }
    public IReadOnlyDictionary<string, string> CustomFields { get; set; }
    // Presentation-specific
    public string? Theme { get; set; }
    public SlideSize Size { get; set; } = SlideSize.Widescreen16x9;
}
```

**Key design principles:**

1. **Slides own Markdig AST fragments.** Each `Slide.Content` is a `MarkdownDocument` containing only that slide's content nodes. This lets us reuse existing transforms (syntax highlighting, math, Mermaid) on per-slide content. **Wei debate caveat:** AST fragment reparenting must be validated via a proof-of-concept spike before implementation begins. If Markdig's parent references or transform invariants break on reparented fragments, the fallback is to re-parse each slide's source text through Markdig independently (Option 3 from ADR-0015). See "Required Spikes" below.

2. **Metadata is typed, not stringly-typed.** Layouts and directives use records with well-known constants (not closed enums — see Wei debate on `SlideLayout`). Animations and directives are typed records.

3. **Explicit separate pipeline.** The PPTX path uses a dedicated `SlidePipeline` orchestrator — not a generalized `ConversionPipeline<T>` and not a fork of the existing pipeline. The DOCX `ConversionPipeline` and `IFormatEmitter` contract remain unchanged. The PPTX emitter implements a new `ISlideEmitter` interface: `Task EmitAsync(SlideDocument doc, ResolvedTheme theme, EmitOptions options, Stream output)`. This is an explicit second pipeline, not a vague "or" in the existing pipeline.

4. **Transforms run on full content first, then distribute.** (Wei debate — scale concern.) Transforms like `MermaidDiagramRenderer`, `SyntaxHighlightAnnotator`, and `MathBlockAnnotator` run once on the full `MarkdownDocument` before slide splitting. Results (annotations via `SetData`) are preserved on nodes when they are distributed into per-slide fragments. This avoids the N× performance penalty of running transforms per-slide.

5. **Extensible for future formats.** A Reveal.js emitter, a Google Slides API emitter, or a Beamer emitter could all consume `SlideDocument`.

6. **Shared metadata interface.** `PresentationMetadata` and `DocumentMetadata` both implement `IDocumentMetadata` to prevent drift. `FrontMatterExtractor` is reused for MARP front matter, with presentation-specific fields handled by `MarpParser`.

## Required Spikes

Before implementation begins, the following must be validated:

1. **AST fragment reparenting.** Create a test that parses a multi-slide MARP deck through Markdig, splits the AST at `ThematicBreakBlock` boundaries, creates per-slide `MarkdownDocument` instances, and runs `SyntaxHighlightAnnotator`, `MermaidDiagramRenderer`, and `MathBlockAnnotator` against them. If any transform fails or produces incorrect results, document the failure mode and switch to re-parsing each slide from source text.

2. **Cross-slide references.** Test what happens when a `LinkReferenceDefinition` on slide 1 is referenced on slide 3. Test footnotes that span slides. If these are common in MARP decks, the slide extractor must handle them (duplicate link ref defs into each slide's fragment, or collect them into a shared context).

## Rejected Alternative: Annotation-Only (No IR)

Wei proposed using `SetData()` annotations on the Markdig AST instead of a separate `SlideDocument`, consistent with how the DOCX path works. This was considered and rejected because:

- The PPTX emitter must iterate slides as top-level units, group content per slide, and map each slide to a different slide master/layout. With annotations, the emitter becomes a "group by slide index, then dispatch by layout, then walk content" triple loop. With `SlideDocument`, it is a straightforward slide-by-slide walk.
- Speaker notes, slide transitions, and build animations are *per-slide* concerns, not per-node concerns. Attaching them to an arbitrary node in the slide (e.g., the first paragraph) is semantically wrong.
- The IR makes the PPTX emitter dramatically simpler at the cost of a more complex parser — a trade-off that favors long-term maintainability since the parser is written once but the emitter is evolved repeatedly.

## Consequences

### Positive

- Clean separation between document semantics (Markdig AST) and presentation structure (SlideDocument).
- Slide layouts, speaker notes, animations, and directives are first-class — not annotations bolted onto a document AST.
- Each slide's content is a standalone Markdig AST fragment, enabling per-slide transforms.
- Future slide-targeting emitters get a ready-made contract.

### Negative

- New IR type adds code and concepts. Developers must understand both `MarkdownDocument` (DOCX path) and `SlideDocument` (PPTX path).
- Two parallel pipelines (`ConversionPipeline` for DOCX, `SlidePipeline` for PPTX) must be maintained. New pipeline features must be validated against both paths. This is an accepted cost — the alternative (one generalized pipeline) was rejected as premature abstraction.
- Shared transforms (highlighting, math, Mermaid) must work on both full `MarkdownDocument` and per-slide fragments. Validated via required spike (see above). Transforms run on the full document before splitting, so per-slide fragments only need to carry annotations, not re-run transforms.
- `SlideDocument` types live in `Md2.Core`, increasing that package's surface area.
- AST fragment reparenting is unproven. If the spike reveals that Markdig nodes cannot be safely reparented, the fallback (re-parsing from source text) adds latency and loses `SetData` annotations, requiring transforms to run per-slide.

### Neutral

- The DOCX pipeline is completely unaffected. No regression risk.
- The `SlideDocument` design does not dictate the parser architecture — ADR-0015 covers that.

## Fitness Functions

- [ ] No PPTX-specific types (Open XML, `PresentationDocument`) appear in `Md2.Core/Slides/` — the IR is format-agnostic.
- [ ] Every `Slide.Content` is a valid `MarkdownDocument` that can be processed by existing `IAstTransform` implementations.
- [ ] `SlideDocument` is not referenced by `Md2.Emit.Docx` — the DOCX path remains independent.
