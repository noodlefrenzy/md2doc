---
agent-notes: { ctx: "ADR for dedicated MARP parser producing SlideDocument", deps: [docs/adrs/0014-slide-document-ir.md, docs/adrs/0003-markdig-markdown-parser.md], state: proposed, last: "archie@2026-03-15", key: ["Wei debate complete — compatibility scope, directive breakdown, and list marker spike required"] }
---

# ADR-0015: Dedicated MARP Parser Architecture

## Status

Proposed

## Context

With `SlideDocument` as the target IR (ADR-0014), we need a parser that converts MARP-styled Markdown into `SlideDocument`. The question is how this parser is structured internally.

MARP Markdown is standard Markdown with additions:
- YAML front-matter with MARP-specific directives (`theme`, `paginate`, `size`, `headingDivider`, etc.)
- `---` as slide separators (overloads the thematic break)
- HTML comments as local/scoped directives (`<!-- class: invert -->`, `<!-- _backgroundColor: aqua -->`)
- Image syntax extensions (`![bg cover](img.jpg)`, `![w:200](img.jpg)`)
- Speaker notes in HTML comments (`<!-- speaker notes here -->`)
- `<!-- fit -->` for auto-scaling headings
- Fragmented lists (`*` for animated, `-` for static)

**Options evaluated:**

1. **Fully custom parser.** Write a Markdown parser from scratch that understands MARP syntax natively. Produces `SlideDocument` directly.

2. **Markdig + post-processing.** Use Markdig to parse the Markdown (reusing all existing extensions), then post-process the Markdig AST to extract slide boundaries, directives, and MARP-specific features, producing `SlideDocument`.

3. **Markdig for content, custom pre-processor for structure.** Pre-process the raw Markdown text to identify slide boundaries and extract directives, then parse each slide's content through Markdig separately.

## Decision

Use **Option 2: Markdig + post-processing** within a new `Md2.Slides` package.

The `MarpParser` class:
1. Parses the full MARP Markdown through Markdig (reusing the existing `Md2MarkdownPipeline` with MARP-specific extensions added).
2. Walks the resulting `MarkdownDocument` to identify slide boundaries (`ThematicBreakBlock` nodes and `headingDivider` logic).
3. Extracts MARP directives from `HtmlBlock` comment nodes.
4. Splits the AST into per-slide `MarkdownDocument` fragments.
5. Assembles a `SlideDocument` with typed metadata.

```
MARP Markdown (string)
  → Markdig parse (reused pipeline + MARP extensions)
  → MarkdownDocument (full AST)
  → MarpSlideExtractor (walks AST, splits at slide boundaries)
  → SlideDocument (list of Slide objects, each with Markdig content fragment)
```

**New package: `Md2.Slides`**

```
Md2.Slides/
  MarpParser.cs                   — Top-level: string → SlideDocument
  Directives/
    MarpDirectiveExtractor.cs     — Extracts directive comments from HtmlBlock nodes
    MarpDirectiveClassifier.cs    — Classifies directives as global, local, or scoped (_prefix)
    MarpDirectiveCascader.cs      — Applies cascading semantics (global → all, local → forward, scoped → current)
  MarpSlideExtractor.cs           — Splits Markdig AST at slide boundaries
  MarpImageSyntax.cs              — Interprets bg, w:, h:, fit, drop-shadow (see Supported Syntax)
  MarpExtensionParser.cs          — Parses md2-specific extensions (<!-- md2: {...} -->)
  SlideLayoutInferrer.cs          — Infers layout from content structure + class directive
  FragmentedListDetector.cs       — Detects * vs - list markers for build animations (requires spike)
```

**(Wei debate: directive handling expanded from 1 class to 3.)** Marpit's official implementation uses 16 sequential plugins; the directive subsystem alone needs extraction, classification (global vs. local vs. scoped), and cascading (forward propagation). A single `MarpDirectiveParser` class would become a 500-line mess.

**Dependencies:**

```
Md2.Slides
  +-- Md2.Core (for SlideDocument types)
  +-- Md2.Parsing (for Md2MarkdownPipeline)
  +-- Markdig (NuGet, transitive via Md2.Parsing)
  +-- YamlDotNet (NuGet, for directive YAML parsing)
```

**Rationale for Option 2 over Option 1 (custom parser):**
- Markdig handles CommonMark + GFM + all our extensions (tables, footnotes, math, code fences, admonitions). Reimplementing this is months of work with no upside.
- MARP's additions are *structural* (slide boundaries, directives) not *syntactic* (new inline/block types). Markdig already parses `---` as `ThematicBreakBlock` and `<!-- ... -->` as `HtmlBlock`. We just need to interpret them differently.
- Per-slide content is standard Markdown. Markdig handles it perfectly.

**Rationale for Option 2 over Option 3 (pre-processor):**
- Pre-processing raw text to find slide boundaries is fragile (what about `---` inside code fences?). Markdig's AST already handles this correctly — `ThematicBreakBlock` is only emitted for actual thematic breaks, not fenced content.
- Parsing each slide separately through Markdig would lose cross-slide context (e.g., a heading numbering scheme, global directive state).

**MARP extension syntax for md2-specific features:**

```markdown
<!-- md2: { build: "bullets", layout: "two-column" } -->
<!-- md2: { chart: { type: "bar", data: "inline" } } -->
```

These are standard HTML comments (Markdig parses them as `HtmlBlock`), containing a `md2:` prefix followed by YAML. A MARP tool would ignore them (they're just comments). The `MarpExtensionParser` extracts and validates the YAML payload.

## MARP Compatibility Scope

**(Wei debate: version pinning and explicit scope required.)**

This parser targets **Marpit v3.x** directive semantics and **Marp Core v3.x** built-in themes. Compatibility scope:

**Fully supported:**
- `---` slide separators
- Front-matter directives: `theme`, `paginate`, `size`, `headingDivider`, `header`, `footer`, `class`, `backgroundColor`, `backgroundImage`, `color`
- Local directives in HTML comments: `<!-- directive: value -->`
- Scoped directives with `_` prefix: `<!-- _class: invert -->`
- Speaker notes in HTML comments
- `<!-- fit -->` auto-scaling headings
- Image sizing: `w:`, `h:`, `width:`, `height:` with px/em/% units
- Background images: `bg`, `bg cover`, `bg contain`, `bg fit`, `bg auto`, `bg left:N%`, `bg right:N%`

**Supported with caveats:**
- Fragmented lists (`*` vs `-` marker distinction) — requires Markdig spike. If marker info is lost, will implement via custom Markdig extension. Documented as a v2.1 feature if spike fails.
- Background image filters (`blur:`, `brightness:`, `sepia`, etc.) — mapped to PPTX artistic effects where available, otherwise ignored with warning.
- Multiple background images — PPTX supports one background per slide. Additional `bg` images rendered as positioned images.

**Not supported (documented in README):**
- CSS-based custom MARP themes (MARP uses CSS; md2 uses YAML themes)
- `@import` rules in style blocks
- Inline `<style>` blocks
- `transition:` directive (PPTX transitions are set via `<!-- md2: { transition: "fade" } -->` extension)

**Rejected alternative: Consume marp-cli HTML output.** (Wei debate.) This was considered and rejected because: (a) it adds a Node.js runtime dependency, (b) HTML output loses AST structure needed for native PPTX tables, charts, and Mermaid shapes, (c) the md2 theme DSL cannot be applied to pre-rendered HTML, and (d) the user's quality bar ("better than Claude") requires native PPTX generation, not HTML-to-PPTX conversion.

## Required Spikes

1. **Fragmented list marker detection.** Parse a list with mixed `*` and `-` markers through Markdig. Inspect `ListBlock.BulletType` and `ListItemBlock` properties. If per-item markers are not preserved, write a custom Markdig extension (`FragmentedListExtension`) that captures the original marker character during parsing and attaches it via `SetData()`.

2. **AST fragment reparenting.** (Shared with ADR-0014.) Validate that Markdig nodes can be detached from one `MarkdownDocument` and attached to another without breaking transform invariants.

## Consequences

### Positive

- Maximum Markdown parsing reuse — zero duplication of CommonMark/GFM/extension parsing.
- Existing transforms (syntax highlighting, math, Mermaid) work on per-slide content fragments without modification.
- `ThematicBreakBlock` handling is robust — no false positives from code fences.
- Extension syntax is backwards-compatible with MARP tools.
- New `Md2.Slides` package cleanly separates slide-awareness from document parsing and PPTX emission.

### Negative

- Splitting a single `MarkdownDocument` AST into per-slide fragments requires careful node detachment. Markdig's AST nodes have parent references that must be handled correctly. Cross-slide `LinkReferenceDefinition` blocks and footnotes need special handling (duplicate into each referencing slide's fragment).
- The `MarpParser` depends on Markdig internals for AST walking — tighter coupling than a clean custom parser.
- Directive cascading is a non-trivial state machine (3 classes, not 1). Global directives propagate to all slides, local directives propagate forward, scoped directives apply to current slide only. This matches Marpit's semantics but is more complex than the original ADR acknowledged.
- MARP syntax is a moving target (Marpit v3 → v4 changes happened). Pinning to v3.x creates an ongoing maintenance burden tracking upstream.
- The image syntax (`MarpImageSyntax`) is a complex mini-DSL with background modes, sizing, filters, and split layouts. This is a meaningful parsing effort, not a single-file task.

### Neutral

- The `Md2.Slides` package is a new dependency for `Md2.Cli` but does not affect `Md2.Emit.Docx` or the DOCX path.
- Future non-MARP slide parsers (e.g., a Slidev parser) would produce `SlideDocument` via a different parser class in the same or a sibling package.

## Fitness Functions

- [ ] `Md2.Slides` does not reference `Md2.Emit.Pptx` or any Open XML types — it is format-agnostic.
- [ ] Every MARP directive test case uses real MARP syntax (not invented syntax) — verified against MARP documentation.
- [ ] `<!-- md2: ... -->` extensions are valid HTML comments that a standard MARP renderer would ignore.
- [ ] `Md2.Slides` does not duplicate any parsing logic from `Md2.Parsing` — it composes, not copies.
