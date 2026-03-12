---
agent-notes:
  ctx: "Sprint 8 plan — Polish + Ship"
  deps: [CLAUDE.md, docs/retrospectives/2026-03-12-sprint-7-retro.md]
  state: active
  last: "grace@2026-03-12"
---
# Sprint 8 Plan — Polish + Ship

**Sprint:** 8
**Date:** 2026-03-12
**Sprint Goal:** md2 v1 is reliable, well-documented at the CLI surface, and validated end-to-end.
**Issues:** 6 (P0 + P1), +2 stretch

## Priority Order

### P0 — Must Do

| # | Issue | Size | Wave | Notes |
|---|-------|------|------|-------|
| 78 | fix(cli): Playwright timeout and cancellation support | M | 1 | Reliability — CLI must not hang |
| 60 | chore: CLI polish, --help refinement, error messages | M | 1 | Release quality — first user interaction |
| 56 | feat(emit-docx): DOCX metadata (subject, keywords) | S | 1 | Completeness — small, high polish-per-effort |
| 57 | test(e2e): comprehensive 20-page document validation | L | 2 | Release confidence — end-to-end validation |

### P1 — Should Do

| # | Issue | Size | Wave | Notes |
|---|-------|------|------|-------|
| 58 | test(visual): preset visual regression snapshots | M | 2 | Quality gate for presets |
| 54 | feat(cli): md2 doctor diagnostic command | M | 2 | Supportability — diagnose missing deps |

### P2 — Stretch

| # | Issue | Size | Wave | Notes |
|---|-------|------|------|-------|
| 34 | perf: Mermaid and math rendering benchmarks | M | 3 | Baseline numbers before v1 |
| 55 | feat(cli): pipeline inspection (--dry-run, --stage) | M | 3 | Developer debugging tool. May need arch gate check. |

### Deferred Past v1

| # | Issue | Size | Rationale |
|---|-------|------|-----------|
| 59 | PPTX emitter stub | S | PPTX is v2 scope |
| 53 | Multi-file concatenation | M | Nice-to-have, not v1 essential |
| 52 | md2 preview with hot-reload | M | Depends on #51, power-user feature |
| 51 | HTML preview server | L | Large, approximation by design (TD-A03) |

## Wave Plan

### Wave 1: Reliability + Polish (3 items, independent)
- **#78** (M) — Playwright timeout/cancellation
- **#60** (M) — CLI polish, --help, error messages
- **#56** (S) — DOCX metadata from front matter

All three are independent, can be parallelized.

### Wave 2: Validation + Diagnostics (3 items)
- **#57** (L) — 20-page e2e validation (depends on #78 for Mermaid stability)
- **#58** (M) — Visual regression snapshots
- **#54** (M) — `md2 doctor` command

#57 benefits from #78. #58 and #54 are independent.

### Wave 3: Stretch (2 items, only if P0+P1 complete)
- **#34** (M) — Benchmarks
- **#55** (M) — Pipeline inspection

## Architecture Gate

No items require the Architecture Gate. #55 may need a check if `ConversionPipeline` doesn't support partial execution, but it's stretch and will be evaluated if reached.

## Tech Debt

No escalations. TD-002, TD-005, TD-006 are all accepted post-v1 by explicit user decision.
