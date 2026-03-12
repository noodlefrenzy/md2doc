---
agent-notes:
  ctx: "Sprint 7 plan — Presets, Extraction, Document Structure"
  deps: [CLAUDE.md, docs/retrospectives/2026-03-12-sprint-6-retro.md]
  state: active
  last: "grace@2026-03-12"
---
# Sprint 7 Plan — Presets, Extraction, Document Structure

**Sprint:** 7
**Date:** 2026-03-12
**Issues:** 10 (9 feature + 1 tech debt escalation)

## Priority Order

### P0 — Tech Debt Escalation (mandatory)

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 76 | refactor(emit-docx): extract shared ExtractInlineText helper | S | **TD-004** — 3-sprint escalation. Grace override authority. |

### P1 — Theme Completeness

| # | Issue | Size | Architecture Gate? |
|---|-------|------|--------------------|
| 43 | feat(themes): 5 built-in style presets | L | No (ADR-0009 covers) |
| 44 | feat(themes): DocxStyleExtractor for template extraction | L | No (ADR-0009 covers) |
| 45 | feat(cli): md2 theme extract command | S | No (ADR-0009 covers) |
| 46 | feat(cli): md2 theme validate and md2 theme list commands | S | No (ADR-0009 covers) |

### P2 — Document Structure

| # | Issue | Size | Architecture Gate? |
|---|-------|------|--------------------|
| 47 | feat(emit-docx): TOC generation with configurable depth and styling | M | No (standard OOXML) |
| 48 | feat(emit-docx): cover page generation from front matter | M | No (standard OOXML) |
| 49 | feat(core): cross-reference linking (heading links to bookmarks) | M | No (standard OOXML) |
| 50 | feat(emit-docx): page headers with document/section title | M | No (standard OOXML) |

### P3 — Carry-forward

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 33 | feat(emit-docx): image captions from alt text or title | S | Carry-forward from Sprint 5 |

## Waves

### Wave 1: Tech Debt + Theme Foundation
- #76 (S) — ExtractInlineText refactor (quick win, unblocks TD-004)
- #43 (L) — 5 built-in presets (foundation for extraction testing)
- #33 (S) — Image captions (quick win, independent)

### Wave 2: Theme Extraction + CLI
- #44 (L) — DocxStyleExtractor (depends on preset examples for testing)
- #45 (S) — md2 theme extract command (CLI wrapper for #44)
- #46 (S) — md2 theme validate + md2 theme list commands

### Wave 3: Document Structure
- #47 (M) — TOC generation
- #48 (M) — Cover page from front matter
- #49 (M) — Cross-reference linking
- #50 (M) — Page headers

## Architecture Gate Check

All Sprint 7 feature issues are covered by existing ADRs:
- ADR-0009 (YAML Theme DSL): covers #43, #44, #45, #46
- ADR-0004 (Open XML SDK): covers #47, #48, #49, #50
- No ADR needed for #33 (image captions) or #76 (refactoring)

No new ADRs needed. No Architecture Gate blocking.

## Notes

- TD-004 is the only escalated debt item. It's a Small task (extract helper method) and shouldn't block other work.
- #43 (presets) is the highest-value item: the presets define the product's visual identity. Pat considers this P1-critical because "style presets are a first-class concern" (product-context.md).
- #44 (DocxStyleExtractor) enables the template → YAML round-trip workflow, which is the "central workflow for corporate template adoption" (ADR-0009).
- Wave 3 items (#47-#50) are independent of each other and can be parallelized.
