---
agent-notes: { ctx: "discovery tracking for PPTX MARP support", deps: [docs/product-context.md, docs/code-map.md], state: active, last: "cam@2026-03-15" }
---

# Discovery: PPTX from MARP-Styled Markdown

**Date:** 2026-03-15
**Lead:** Cam
**Status:** Active
**Prior Phase:** None

## Key Decisions

- MARP-compatible Markdown is the input format — not prose docs chunked into slides
- Full MARP compatibility target (gaps documented, not silently dropped)
- Mermaid flowcharts as native PPTX shapes first, image fallback for complex diagram types
- Build slides (bullet-by-bullet reveal) only for animations — no entrance/exit/motion
- Speaker notes via existing MARP `<!-- ... -->` syntax, no extension needed
- Extensions via structured comments for features MARP doesn't cover (animations, chart data, layout hints)
- Extensions must be backwards-compatible — MARP tools should still render the deck (ignoring extensions)
- Shared YAML theme DSL across DOCX and PPTX — same presets (e.g. nightowl), extended schema for PPTX-specific properties
- PPTX template → YAML extraction capability (reverse theme engineering)
- Quality bar: must be demonstrably better than "ask Claude to convert my MARP"
- Edit-readiness is paramount — clean XML, editable shapes, real text runs, pleasant to edit in PowerPoint

## Artifacts Produced

- This discovery artifact
- Updated memory: `project_pptx_direction.md`

## Open Questions

- Which MARP directives map cleanly to PPTX and which need caveats?
- How should the theme schema be extended — additive section or interleaved properties?
- What's the structured comment syntax for extensions (HTML comments, YAML blocks, other)?
- Which slide layouts to support in v1 (title, content, two-column, section divider, blank)?
- Chart-from-data: what data format in code fences? (CSV, JSON, YAML?)

## Next Phase

- Phase 1b: Pat confirms product-context.md is still accurate
- Phase 2: Sacrificial concepts (Dani)
