---
agent-notes:
  ctx: "technical debt register, persists across sprints"
  deps: [docs/plans/v1-implementation-plan.md]
  state: active
  last: "grace@2026-03-12"
  key: ["Grace tracks, Pat prioritizes against features", "3+ sprint debt auto-escalated to P0"]
---
# Technical Debt Register

<!-- Grace maintains this register. Pat prioritizes debt against feature work. -->
<!-- This persists across sprints -- board items get closed, but debt lives here until resolved. -->
<!-- Debt open for 3+ sprints is automatically escalated by Grace (override authority per persona definition). -->

**Project:** md2
**Last reviewed:** 2026-03-12 (Sprint 5 boundary)

## Active Debt

| ID | Description | Category | Incurred | Why (business reason) | Est. cost to fix | Risk if left | Sprint to fix | Status |
|----|-------------|----------|----------|----------------------|-----------------|-------------|--------------|--------|
| TD-001 | Hardcoded default ResolvedTheme in Md2.Emit.Docx (Issue 13) | Hardcoded values | Sprint 2 | Theme engine not built yet; need working DOCX output before cascade exists | S (swap to real cascade) | Low: replaced in Sprint 6 | Sprint 6 | **ESCALATED P0** -- 3-sprint threshold (Sprint 2→5). Resolution: #36 ThemeCascadeResolver |
| TD-002 | FrontMatterExtractor in Md2.Core assembly with Md2.Parsing namespace | Architecture smell | Sprint 1 | Avoids circular dependency between Core and Parsing | S (move to shared types project if needed) | Low: works correctly, documented | Post-v1 | Open -- accepted |
| TD-003 | No logging framework configured | Missing infrastructure | Sprint 2 | Focused on core functionality first | M (add ILogger, wire to --verbose) | Medium: harder to debug in production | Sprint 5 | **RESOLVED** -- #71 wired Microsoft.Extensions.Logging |
| TD-004 | ExtractInlineText duplicated across TableBuilder, ListBuilder, DocxAstVisitor | Copy-paste duplication | Sprint 3 | Each builder needed inline text extraction; no shared utility existed yet | S (extract to shared static helper) | Low: identical logic, divergence risk | Post-v1 | Open -- accepted |
| TD-005 | Md2.Emit.Docx references Md2.Parsing for AdmonitionBlock type | Architecture coupling | Sprint 4 | Need AdmonitionBlock type in emitter; no shared abstractions project exists | S (shared abstractions project) | Medium: coupling grows if more custom types added | Post-v1 | Open -- accepted |
| TD-006 | BrowserManager.GetBrowserAsync null-check not synchronized | Concurrency bug | Sprint 5 | Single-threaded CLI doesn't expose the race; fixing adds complexity for no current benefit | S (add SemaphoreSlim or Lazy<Task<T>>) | Low: CLI is single-threaded | Post-v1 | Open -- accepted |

## Anticipated Debt (from plan)

These are known shortcuts planned in the implementation. They are not yet incurred but are logged here proactively so they are not forgotten.

| ID | Description | Category | Expected Sprint | Resolution Sprint | Notes |
|----|-------------|----------|----------------|-------------------|-------|
| TD-A01 | Default preset authored as in-memory hardcoded values before YAML engine exists | Hardcoded values | Sprint 2 | Sprint 6 | Replaced when PresetRegistry loads YAML files |
| TD-A02 | Math graceful degradation renders LaTeX as code span (not a styled "math unavailable" placeholder) | Missing polish | Sprint 5 | Post-v1 | Acceptable for v1; code fallback is clear |
| TD-A03 | Preview HTML is an approximation of DOCX output, not pixel-perfect | Missing polish | Sprint 8 | Ongoing | By design; accepted trade-off |

## Resolved Debt

| ID | Description | Incurred | Resolved | How it was fixed |
|----|-------------|----------|----------|-----------------|
| TD-003 | No logging framework configured | Sprint 2 | Sprint 5 | #71: Microsoft.Extensions.Logging wired with --debug flag, 27 log calls across 5 files |

## Debt Categories

Tag each debt item to track patterns:

| Category | Count | Trend |
|----------|-------|-------|
| Missing tests | 0 | -- |
| Hardcoded values | 1 | -- |
| Missing error handling | 0 | -- |
| Copy-paste duplication | 1 | New |
| Outdated dependencies | 0 | -- |
| Missing docs | 0 | -- |
| Performance | 0 | -- |
| Security | 0 | -- |
| Missing polish | 0 | -- |

## Escalation Rules

- **Sprint boundary:** Grace reviews the register. New debt discovered during the sprint is added. Pat decides what to pay down next sprint.
- **3-sprint threshold:** Any debt open for 3+ sprints is automatically escalated to P0 by Grace. This overrides Pat's prioritization. The only exception is an explicit user decision to defer.
- **Every 3 sprints:** Full debt review. Re-estimate costs. Re-assess risks.

## Review Cadence

| Checkpoint | Action | Who |
|------------|--------|-----|
| After each sprint | Add new debt, update statuses | Grace |
| Sprint boundary | Prioritize debt vs. features for next sprint | Pat + Grace |
| Every 3 sprints | Full review, escalation check | Grace (with override authority) |
| Pre-release | Final debt audit -- nothing critical ships unresolved | Grace + Pat + Vik |
