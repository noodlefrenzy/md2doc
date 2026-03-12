---
agent-notes:
  ctx: "Sprint 6 plan — Theme Engine"
  deps: [CLAUDE.md, docs/retrospectives/2026-03-12-sprint-5-retro.md]
  state: active
  last: "grace@2026-03-12"
---
# Sprint 6 Plan — Theme Engine

**Sprint:** 6
**Date:** 2026-03-12
**Issues:** 10 (8 original Sprint 6 + 1 process-improvement carry-forward + 1 README fix)

## Priority Order

### P0 — Tech Debt Escalation (mandatory)

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 36 | feat(themes): ThemeCascadeResolver with 4-layer merge | L | **Resolves TD-001** (3-sprint escalation). Replaces hardcoded ResolvedTheme.CreateDefault(). Depends on #35. |

TD-001 has been open since Sprint 2 (3 sprints). Grace escalation authority: this MUST be P0 and cannot be deprioritized.

### P1 — Theme Engine Core

| # | Issue | Size | Architecture Gate? |
|---|-------|------|--------------------|
| 35 | feat(themes): ThemeParser and ThemeDefinition model with YamlDotNet | L | No (ADR-0009 exists) |
| 37 | feat(themes): template safety (IRM detection, .doc rejection, .docm warning, size limit) | M | No (ADR-0010 exists) |
| 38 | feat(themes): PresetRegistry with embedded preset YAML loading | M | No |
| 39 | feat(themes): ThemeValidator with schema checking and line numbers | M | No |
| 40 | feat(cli): md2 theme resolve command | M | No |
| 41 | feat(cli): --preset, --theme, --template, --style flags on convert command | S | No |
| 42 | feat(cli): --verbose shows cascade resolution details and timing | M | No |

### P2 — Process / Documentation

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 74 | fix: README project structure and features list accuracy | S | P1 from Diego 5-minute test |
| 69 | process: evaluate shared types for Markdig custom extensions | — | Process-improvement carry-forward (2 sprints) |

## Waves

### Wave 1: Foundation + README Fix
- #74 (S) — README accuracy fix (quick win, unblocks Diego gate)
- #35 (L) — ThemeParser + ThemeDefinition model (foundation for everything else)
- #38 (M) — PresetRegistry (loads preset YAML; needed by cascade resolver)

### Wave 2: Cascade + Safety
- #36 (L) — ThemeCascadeResolver (resolves TD-001, depends on #35 + #38)
- #37 (M) — Template safety (IRM, .doc, .docm, size limit)

### Wave 3: Validation + CLI
- #39 (M) — ThemeValidator
- #40 (M) — `md2 theme resolve` command
- #41 (S) — Wire --preset/--theme/--template/--style flags

### Wave 4: Verbose + Process
- #42 (M) — --verbose cascade details and timing
- #69 (process) — Evaluate shared types

## Architecture Gate Check

All Sprint 6 feature issues have existing ADRs:
- ADR-0009 (YAML Theme DSL): covers #35, #36, #38, #39, #40, #41, #42
- ADR-0010 (IRM Protected Templates): covers #37

No new ADRs needed. No Architecture Gate blocking.

## Notes

- The Md2.Themes project is referenced in code-map.md but doesn't exist yet — this sprint creates it.
- TD-001 resolution is the primary goal. Everything else builds on the cascade resolver.
- #69 (shared types) evaluation: assess whether adding a shared abstractions project is warranted now that TD-005 exists and theme types will add more cross-boundary types. Decision may result in a new ADR.
