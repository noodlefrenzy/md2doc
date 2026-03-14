---
agent-notes:
  ctx: "Sprint 11 plan — Release Preparation"
  deps: [CLAUDE.md, docs/retrospectives/2026-03-13-sprint-10-retro.md]
  state: complete
  last: "grace@2026-03-14"
---
# Sprint 11 Plan — Release Preparation

**Sprint:** 11
**Date:** 2026-03-13
**Sprint Goal:** Dependency hygiene, documentation polish, and theme-aware rendering for v1 release.

**Rationale:** All v1 features and process debt are complete. Sprint 11 addresses pre-release findings from the Pierrot dependency health audit and Diego README test, plus user-requested theme improvements discovered during preset review. This is the final sprint before v1 release.

## Priority Order

### P0 — Must Do

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 86 | chore(deps): upgrade Playwright, Markdig, System.CommandLine, TextMateSharp.Grammars, OpenXml | M | Pierrot pre-release audit HIGH findings |
| 87 | docs: update SBOM with actual versions, missing deps, CVE table | S | Pierrot audit finding |

### P1 — Should Do

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 89 | feat(diagrams): theme-aware Mermaid rendering via themeVariables | M | User-confirmed design: base theme + themeVariables mapping from ResolvedTheme |
| 90 | fix(emit-docx): code block text contrast handling for dark-background themes | M | User-flagged: invisible code in hackterm/dark presets |
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

### Wave 2: Theme-Aware Rendering + Contrast Fix (2 items)

| # | Issue | Size | Dependencies |
|---|-------|------|-------------|
| 89 | Theme-aware Mermaid rendering | M | After #86 (deps may affect Playwright) |
| 90 | Code block contrast handling | M | None |

### Wave 3: Documentation (1 item)

| # | Issue | Size | Dependencies |
|---|-------|------|-------------|
| 88 | README updates | S | After #89/#90 (new features to document) |

## Architecture Gate

#89 (Mermaid theming) touches pipeline ordering and cross-package data flow. Design doc exists at `docs/research/mermaid-theme-aware-rendering.md`. ADR + Wei debate required before implementation.

## Tech Debt

TD-002, TD-005, TD-006 remain user-deferred post-v1. No new debt expected.
