---
agent-notes:
  ctx: "Sprint 11 plan — Release Preparation"
  deps: [CLAUDE.md, docs/retrospectives/2026-03-13-sprint-10-retro.md]
  state: active
  last: "grace@2026-03-13"
---
# Sprint 11 Plan — Release Preparation

**Sprint:** 11
**Date:** 2026-03-13
**Sprint Goal:** Dependency hygiene and documentation polish for v1 release.

**Rationale:** All v1 features and process debt are complete. Sprint 11 addresses pre-release findings from the Pierrot dependency health audit and Diego README test. This is the final sprint before v1 release.

## Priority Order

### P0 — Must Do

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 86 | chore(deps): upgrade Playwright, Markdig, System.CommandLine, TextMateSharp.Grammars, OpenXml | M | Pierrot pre-release audit HIGH findings |
| 87 | docs: update SBOM with actual versions, missing deps, CVE table | S | Pierrot audit finding |

### P1 — Should Do

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 88 | docs: update README with preview, doctor, theme commands + project structure | S | Diego 5-minute test P2/P3 findings |

### P2 — Stretch

No stretch items. Ship after P0 + P1.

### Deferred Post-v1

| # | Issue | Rationale |
|---|-------|-----------|
| 59 | PPTX emitter stub | v2 scope |
| 53 | Multi-file concatenation | Nice-to-have |
| 55 | Pipeline inspection | Developer tool |

## Wave Plan

### Wave 1: Dependency Upgrades + SBOM (2 items)

| # | Issue | Size | Dependencies |
|---|-------|------|-------------|
| 86 | Dependency upgrades | M | None |
| 87 | SBOM update | S | After dependency upgrades |

### Wave 2: Documentation (1 item)

| # | Issue | Size | Dependencies |
|---|-------|------|-------------|
| 88 | README updates | S | None |

## Architecture Gate

No items require the Architecture Gate. All work is maintenance/docs.

## Tech Debt

TD-002, TD-005, TD-006 remain user-deferred post-v1. No new debt expected.
