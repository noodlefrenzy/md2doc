---
agent-notes: { ctx: "plan tracking for PPTX MARP support", deps: [docs/tracking/2026-03-15-pptx-marp-architecture.md], state: active, last: "grace@2026-03-15" }
---

# Plan: PPTX from MARP-Styled Markdown

**Date:** 2026-03-15
**Lead:** Grace
**Status:** Active
**Prior Phase:** [Architecture](2026-03-15-pptx-marp-architecture.md)

## Key Decisions

- 3 waves, 6 sprints, 47 issues (#104-#150)
- Branch strategy: `pptx/v2` integration branch with feature branches, periodic `main` merges, final PR to `main`
- v2.0 scope: MARP parsing, PPTX output, themes, build animations, headers/footers (Sprints 1-4, 6)
- v2.1 scope: Mermaid native shapes, charts from data (Sprint 5)
- 2 required spikes before Sprint 2 (AST reparenting, fragmented list markers)
- Test strategy extended with PPTX appendix (Md2.Slides.Tests, Md2.Emit.Pptx.Tests)
- Tech debt register updated with 4 anticipated v2 debt items

## Artifacts Produced

- `docs/plans/v2-pptx-implementation-plan.md` — full sprint breakdown
- `docs/test-strategy.md` — Appendix C (PPTX test strategy)
- `docs/tech-debt.md` — v2 anticipated debt
- GitHub Project #15 ("md2 v2 — PPTX") — 47 issues, Sprint 1 in Ready
- Sprint labels: sprint:v2-1 through sprint:v2-6, pptx

## Open Questions

- Chart data format (CSV vs YAML vs JSON) — deferred to Sprint 5
- Mermaid diagram type detection for shape vs image — deferred to Sprint 5
- Custom Markdig extension for fragmented lists — depends on Sprint 1 spike

## Next Phase

- Sprint 1 execution (Wave 1: Foundation)
