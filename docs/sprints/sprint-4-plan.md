---
agent-notes: { ctx: "Sprint 4 wave plan — code blocks, blockquotes, footnotes, fixes", deps: [docs/retrospectives/2026-03-11-sprint-3-retro.md], state: active, last: "grace@2026-03-12" }
---

# Sprint 4 Plan

**Date:** 2026-03-12
**Sprint scope:** Issues 20-26, 65-67
**Total items:** 10 (2L + 4M + 4S)

## Priorities

P1 (blocking from Sprint 3 retro):
- #65 (S) fix: wire --quiet flag
- #66 (S) fix: ImageBuilder exception swallowing
- #67 (S) docs: README quick-start

Feature work:
- #20 (L) CodeBlockBuilder with mono font, background shading
- #21 (L) TextMateSharp syntax highlighting
- #22 (M) Blockquotes with colored left border, nesting
- #23 (M) Footnotes with bidirectional navigation
- #24 (M) Admonitions/callouts with typed styling
- #25 (M) Definition lists
- #26 (S) Horizontal rules / thematic breaks

## Wave Plan

### Wave 1 — Quick Fixes + Thematic Breaks (4S)
Issues: #65, #66, #67, #26
Rationale: Clear P1 debt first, plus #26 is the simplest feature item.

### Wave 2 — Code Blocks + Syntax Highlighting (2L)
Issues: #20, #21
Rationale: #21 depends on #20 (CodeBlockBuilder must exist before highlighting can color its runs). These are the largest items and benefit from consecutive execution.

### Wave 3 — Block Containers (3M)
Issues: #22, #24, #25
Rationale: Blockquotes, admonitions, and definition lists are all block-level containers with similar visitor dispatch patterns. Can share learnings.

### Wave 4 — Footnotes (1M)
Issues: #23
Rationale: Footnotes are architecturally distinct (bidirectional hyperlinks, end-of-document section). Isolated to avoid cross-contamination.

## Exit Criteria
- All 10 issues Done on board
- All tests pass
- code-map.md updated
- Sprint boundary triggered
