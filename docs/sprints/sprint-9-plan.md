---
agent-notes:
  ctx: "Sprint 9 plan — Preview + Validation + Operational Fixes"
  deps: [CLAUDE.md, docs/retrospectives/2026-03-13-sprint-8-retro.md]
  state: active
  last: "grace@2026-03-13"
---
# Sprint 9 Plan — Preview + Validation + Operational Fixes

**Sprint:** 9
**Date:** 2026-03-13
**Sprint Goal:** md2 v1 has a working preview mode, end-to-end validation, and no Below Foundation operational gaps.

**Rationale:** Preview was incorrectly deferred in Sprint 8 and must be restored as core scope. The 20-page e2e validation (#57) has been P0 for two sprints without being started. The two Below Foundation findings from the Sprint 8 operational audit are fast fixes that reduce risk of silent failures.

## Priority Order

### P0 — Must Do

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 57 | test(e2e): comprehensive 20-page representative document validation | L | Carry-forward from Sprint 8 P0. Release confidence gate. |
| 80 | fix(cli): surface TransformContext.Warnings to user | S-M | Below Foundation: silent rendering failures invisible to users. |
| 81 | fix(themes): ThemeParseException should extend Md2Exception | S | Below Foundation: bypasses user-facing error handling. |

### P1 — Should Do

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 51 | feat(preview): HTML preview server with hot-reload via Playwright | L | Restored from incorrect Sprint 8 deferral. Feature Area 8 core scope. |
| 52 | feat(cli): md2 preview command with hot-reload | M | Wires preview server to CLI. Depends on #51. |
| 54 | feat(cli): md2 doctor diagnostic command | M | Carry-forward from Sprint 8 P1. Supportability. |

### P2 — Stretch

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 58 | test(visual): preset visual regression snapshots | M | Carry-forward. Important but not blocking release. |
| 34 | perf: Mermaid and math rendering benchmarks | M | Carry-forward stretch. Baseline numbers before v1. |

### Deferred Past v1 (Confirmed)

| # | Issue | Rationale |
|---|-------|-----------|
| 59 | PPTX emitter stub | PPTX is v2 scope |
| 53 | Multi-file concatenation | Nice-to-have, not v1 essential |
| 55 | Pipeline inspection (--dry-run, --stage, --emit) | Developer debugging tool, not user-facing |

## Scope Changes vs. Original Plan

Per Scope Reduction Gate (docs/process/team-governance.md):

| Item | Original Plan | Sprint 9 Status | Change | Justification |
|------|--------------|-----------------|--------|---------------|
| #51 (Preview server) | Sprint 8 core | P1 | Restored | Incorrectly deferred in Sprint 8. Human confirmed core. |
| #52 (Preview CLI) | Sprint 8 core | P1 | Restored | Depends on #51, same justification. |
| #57 (20-page e2e) | Sprint 8 P0 | P0 | Carry-forward | Not started in Sprint 8. |
| #54 (md2 doctor) | Sprint 8 P1 | P1 | Carry-forward | Not started in Sprint 8. |
| #58 (Visual regression) | Sprint 8 P1 | P2 | Demoted to stretch | Conservative sprint sizing per retro. Does not block release. |
| #55 (Pipeline inspection) | Sprint 8 P2 | Deferred post-v1 | Demoted | Developer tool, not user-facing. |
| #80, #81 | Not in original plan | P0 | Added | Operational audit findings, process-improvement gate requires scheduling. |

## Wave Plan

### Wave 1: Operational Fixes + E2E Foundation (4 items)

| # | Issue | Size | Dependencies |
|---|-------|------|-------------|
| 80 | fix(cli): surface TransformContext.Warnings to user | S-M | None |
| 81 | fix(themes): ThemeParseException should extend Md2Exception | S | None |
| 57 | test(e2e): comprehensive 20-page document validation | L | Benefits from #80 (warnings visible during test runs) |
| 54 | feat(cli): md2 doctor diagnostic command | M | None |

**Execution:** #80 and #81 first (fast fixes). Then #57 and #54 in parallel.

### Wave 2: Preview (2 items, sequential)

| # | Issue | Size | Dependencies |
|---|-------|------|-------------|
| 51 | feat(preview): HTML preview server with hot-reload | L | Playwright infra exists from Sprint 5 |
| 52 | feat(cli): md2 preview command with hot-reload | M | Depends on #51 |

### Wave 3: Stretch (2 items, independent)

| # | Issue | Size | Dependencies |
|---|-------|------|-------------|
| 58 | test(visual): preset visual regression snapshots | M | None |
| 34 | perf: Mermaid and math rendering benchmarks | M | None |

Only attempted if Waves 1 and 2 are complete.

## Architecture Gate

| # | Issue | Needs Gate? | Rationale |
|---|-------|------------|-----------|
| 57 | 20-page e2e | No | Test infrastructure |
| 80 | Surface warnings | No | Wiring existing data to existing output |
| 81 | ThemeParseException fix | No | Exception hierarchy fix |
| 51 | Preview server | **Check** | New project, HTTP server, HTML rendering path. Confirm existing ADR/plan coverage before implementation. |
| 52 | Preview CLI | No | CLI wiring for #51 |
| 54 | md2 doctor | No | CLI diagnostic |

**Action:** Before Wave 2, confirm ADR coverage for preview architecture. If none exists, Archie writes one before implementation.

## Sprint Sizing Rationale

Sprint 8 showed 6 P0+P1 items is the realistic ceiling, and context can be consumed by unplanned work. Sprint 9 plans 6 P0+P1 items across 2 waves. If only Wave 1 completes, we still ship e2e validation, operational fixes, and md2 doctor — meaningful value. Preview carries forward cleanly.

## Tech Debt

No escalations. TD-002, TD-005, TD-006 remain user-deferred post-v1.
