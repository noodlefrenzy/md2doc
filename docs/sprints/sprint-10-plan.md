---
agent-notes:
  ctx: "Sprint 10 plan — Process Cleanup + Release Readiness"
  deps: [CLAUDE.md, docs/retrospectives/2026-03-13-sprint-9-retro.md]
  state: active
  last: "grace@2026-03-13"
---
# Sprint 10 Plan — Process Cleanup + Release Readiness

**Sprint:** 10
**Date:** 2026-03-13
**Sprint Goal:** All process debt cleared, v1 release-ready.

**Rationale:** v1 features are complete. Sprint 10 clears process-improvement debt, adds overdue test coverage, and prepares for release. This is expected to be the final sprint before v1 release.

## Priority Order

### P0 — Must Do

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 85 | test: inline images and Mermaid caption edge cases | S | 3 sprints overdue per process-improvement escalation |
| 83 | process: retroactive Wei debate for ADR-0012 | S | Architecture Gate compliance |

### P1 — Should Do

No P1 items. All v1 features are complete.

### P2 — Stretch

| # | Issue | Size | Notes |
|---|-------|------|-------|
| — | Pre-release audit: Vik dead code pass + Pierrot dependency health | M | Every 3 sprints and before release |

### Deferred Post-v1

| # | Issue | Rationale |
|---|-------|-----------|
| 59 | PPTX emitter stub | v2 scope |
| 53 | Multi-file concatenation | Nice-to-have |
| 55 | Pipeline inspection | Developer tool |

## Wave Plan

### Wave 1: Process Debt (2 items, independent)

| # | Issue | Size | Dependencies |
|---|-------|------|-------------|
| 85 | test: inline image + Mermaid caption integration tests | S | None |
| 83 | process: Wei debate for ADR-0012 | S | None |

### Wave 2: Release Readiness (stretch)

Pre-release audit: dead code pass, dependency health, final test run.

## Architecture Gate

No items require the Architecture Gate. All work is test/process.

## Tech Debt

TD-002, TD-005, TD-006 remain user-deferred post-v1. No new debt expected.
