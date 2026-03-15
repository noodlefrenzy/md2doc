---
agent-notes: { ctx: "architecture tracking for PPTX MARP support", deps: [docs/tracking/2026-03-15-pptx-marp-discovery.md], state: active, last: "archie@2026-03-15" }
---

# Architecture: PPTX from MARP-Styled Markdown

**Date:** 2026-03-15
**Lead:** Archie
**Status:** Active
**Prior Phase:** [Discovery](2026-03-15-pptx-marp-discovery.md)

## Key Decisions

- Chose Concept B (Two-Stage Rocket) — dedicated MARP parser producing `SlideDocument` IR, shared infrastructure for transforms/themes. User values extensibility for future formats over lower maintenance cost.
- `SlideDocument` IR lives in `Md2.Core/Slides/` — format-agnostic contract between parser and emitter.
- `SlideLayout` is an open record type (not enum) — supports custom MARP classes without a closed set.
- Explicit `SlidePipeline` orchestrator — not a generalized pipeline. Two pipelines, accepted maintenance cost.
- Transforms run on full document before slide splitting — avoids N× perf penalty at scale.
- `PresentationMetadata` shares `IDocumentMetadata` interface with `DocumentMetadata` — prevents drift.
- MARP parser uses Markdig internally (Option 2) — reuses all existing extensions. Not a custom parser.
- Directive handling split into 3 classes: extraction, classification, cascading — matches Marpit complexity.
- Compatibility target: Marpit v3.x. Unsupported features documented (CSS themes, @import, inline styles).
- Unified theme YAML with `pptx:` section — shared `colors`/`typography` with per-format overrides.
- `ResolvedPptxTheme` is a mandatory sub-object on `ResolvedTheme` — no flat PPTX fields.
- MARP `theme:` directive is a hint for preset selection, not a cascade layer.
- PPTX template extraction deferred to post-v2.
- Preview is DOCX-only for v2.

## Artifacts Produced

- ADR-0014: SlideDocument IR
- ADR-0015: MARP Parser Architecture
- ADR-0016: Unified Theme Schema Extension for PPTX
- Debate tracking: `2026-03-15-pptx-marp-debate.md`
- Threat model update: 4 new DFDs, 16 new STRIDE entries

## Open Questions

- AST fragment reparenting spike — does it work with existing transforms? (Required before implementation)
- Fragmented list marker spike — does Markdig preserve per-item `*` vs `-`? (Required before implementation)
- Chart data format — CSV, JSON, or YAML in code fences? (Defer to implementation)
- Mermaid native shape coverage — which diagram types map to PPTX shapes? (Defer to implementation)

## Next Phase

- Phase 4: Acceptance Criteria (Pat)
- Phase 5: Planning (Grace + Tara)
