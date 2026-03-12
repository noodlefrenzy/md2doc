---
agent-notes:
  ctx: "Sprint 8 Wave 1 overnight report for human review"
  deps: []
  state: active
  last: "grace@2026-03-12"
---
# Overnight Report — 2026-03-12 (Session B)

**Operator:** Claude (Pat as product proxy)
**Sprint:** 8 (Wave 1 complete)
**Test count:** 536 → ~549 (+13 new tests)
**Commits:** 4 new commits pushed to main

## What Got Done

### Sprint 7 Boundary (complete)
- Retrospective written with full compliance audit
- Board health: 11/11 items followed status flow
- Architecture gate: compliant (no new decisions, all covered by existing ADRs)
- Tech debt register reviewed — no escalations
- Sprint 8 plan created: 6 P0+P1 items in 2 waves, 2 stretch items
- Board updated: P0+P1 items set to Ready

### Sprint 8 Wave 1 — Reliability + Polish (3 items)

| # | Title | Size | Tests | Key Change |
|---|-------|------|-------|------------|
| 56 | feat: DOCX metadata (subject, keywords) | S | +5 | Fixed guard condition that silently dropped properties when only subject/keywords present |
| 78 | fix: Playwright timeout/cancellation | M | +8 | CancellationToken from Ctrl+C → pipeline → transforms → renderers. 30s timeouts. User-friendly Chromium-not-found. |
| 60 | chore: CLI polish | M | 0 | Executable name `md2`, `--cover` flag, clean preset error messages |

### CLI Commands Now Available
```
md2 <input.md>                    # Convert
md2 --toc --cover                 # With TOC and cover page (NEW: --cover)
md2 --preset corporate            # Use preset
md2 --theme custom.yaml           # Custom theme
md2 --style colors.primary=FF0000 # CLI override
md2 theme list                    # List presets
md2 theme resolve                 # Debug cascade
md2 theme extract template.docx   # Extract styles
md2 theme validate custom.yaml    # Validate theme
```

### Bug Fixed
The DOCX metadata guard condition (`SetDocumentProperties`) was checking only Title and Author, causing Subject and Keywords to be silently dropped when no Title/Author was present. Fixed and tested.

## Proxy Decisions (Review Required)

Pat made no proxy decisions during this session. All work followed the sprint plan.

## What's NOT Done

- Sprint 8 Wave 2 (#57, #58, #54) — next session
- Sprint 8 Wave 3 (#34, #55) — stretch, if capacity permits
- Deferred past v1: #59 (PPTX stub), #53 (multi-file), #52/#51 (preview)

## Stats
- **Total tests:** ~549 (all green)
- **New code:** ~200 lines implementation + ~100 lines tests
- **Open tech debt:** 3 items (all post-v1)
- **Sprint 8 velocity so far:** 3/6 P0+P1 items (Wave 1 complete)
