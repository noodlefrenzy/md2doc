---
agent-notes:
  ctx: "Sprint 5 plan — Math, Mermaid, and operational baseline"
  deps: [CLAUDE.md, docs/retrospectives/2026-03-12-sprint-4-retro.md]
  state: active
  last: "grace@2026-03-12"
---
# Sprint 5 Plan — Math + Mermaid + Operational Baseline

**Sprint:** 5
**Date:** 2026-03-12
**Issues:** 12 (8 original Sprint 5 + 3 P1 operational baseline + 1 process-improvement)

## Priority Order

### P0 — Operational Baseline (blocking gate from Sprint 4)

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 71 | feat(core): wire Microsoft.Extensions.Logging with --debug flag | M | Resolves TD-003 (3-sprint escalation at Sprint 5 boundary) |
| 72 | fix(core): consistent error handling with Md2Exception base type | M | P1 from Ines audit |
| 73 | fix: README project structure accuracy | S | Already fixed in boundary commit, close immediately |

### P1 — Math + Mermaid (original Sprint 5 scope)

| # | Issue | Size | Notes | Architecture Gate? |
|---|-------|------|-------|--------------------|
| 27 | feat(diagrams): BrowserManager for shared Playwright/Chromium lifecycle | L | ADR-0008 exists | No (ADR done) |
| 28 | feat(diagrams): MermaidRenderer with PNG output and content-hash caching | L | | No |
| 29 | feat(diagrams): MermaidDiagramRenderer AST transform | M | | No |
| 30 | feat(math): LaTeX to OMML via KaTeX MathML + MML2OMML.xsl | L | ADR-0006 exists | No (ADR done) |
| 31 | feat(math): MathBlockAnnotator AST transform | M | | No |
| 32 | feat(emit-docx): MathBuilder for OMML element insertion | M | | No |
| 33 | feat(emit-docx): image captions from alt text or title | S | | No |
| 34 | perf: Mermaid and math rendering benchmarks | M | | No |

### P2 — Process

| # | Issue | Size | Notes |
|---|-------|------|-------|
| 69 | process: evaluate shared types for Markdig custom extensions | — | Process-improvement carry-forward |

## Waves

### Wave 1: Operational Baseline (P0)
- #73 (S) — close immediately (already fixed)
- #71 (M) — logging framework
- #72 (M) — error handling consistency

### Wave 2: Browser Infrastructure
- #27 (L) — BrowserManager (shared Playwright lifecycle)

### Wave 3: Mermaid + Math Rendering
- #28 (L) — MermaidRenderer
- #29 (M) — MermaidDiagramRenderer transform
- #30 (L) — LaTeX to OMML converter
- #31 (M) — MathBlockAnnotator transform

### Wave 4: Emitter Integration + Polish
- #32 (M) — MathBuilder for OMML insertion
- #33 (S) — Image captions
- #34 (M) — Performance benchmarks
- #69 (process) — Evaluate shared types

## Notes

- TD-001 (hardcoded ResolvedTheme) will hit 3-sprint escalation at Sprint 5 boundary if not resolved. Planned resolution in Sprint 6 (theme engine). Flag to user at next boundary.
- TD-003 (no logging) resolved by #71.
- Playwright/Chromium required for Mermaid and Math. DevContainer already configured with Playwright.
