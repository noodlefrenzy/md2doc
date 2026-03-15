---
agent-notes:
  ctx: "retro on DOCX assumptions leaking into format-neutral Core"
  deps: [CLAUDE.md, docs/process/team-governance.md, docs/process/gotchas.md]
  state: active
  last: "grace@2026-03-15"
---
# Retrospective: Architectural Drift — DOCX Assumptions in Core

**Date:** 2026-03-15
**Scope:** Cross-sprint (Sprints 1–11)
**Trigger:** PPTX v2 planning revealed format-specific assumptions baked into `Md2.Core`

## Context

During PPTX v2 planning, the team (Archie, Wei, Cam, Pat) analyzed the existing architecture for PPTX readiness. Despite the architecture doc (section 9) explicitly planning for PPTX with format-agnostic seam points, DOCX-specific assumptions had leaked into shared types throughout Sprints 1–11.

**Evidence:**

| File | DOCX-Specific Leakage |
|------|-----------------------|
| `ResolvedTheme.cs` | `BlockquoteIndentTwips`, `PageWidth`, `PageHeight`, `MarginTop/Bottom/Left/Right` — all DOCX units/concepts |
| `EmitOptions.cs` | `IncludeToc`, `IncludeCoverPage`, `PageSize`, `Margins` — document-only concepts |
| `ThemeDefinition.cs` | `Docx` property exists, no `Pptx` counterpart despite architecture doc claiming one |
| `MathBlockAnnotator.cs` | Produces OMML (WordprocessingML format); PPTX needs DrawingML math |
| `default.yaml` preset | `docx:` section only, no `pptx:` section despite architecture doc claiming it exists |

## What Went Well

1. **`IFormatEmitter` was built clean.** The top-level emitter interface is genuinely format-agnostic. The architectural seam exists where it was planned.
2. **Transform pipeline is format-neutral.** AST transforms annotate via `SetData`/`GetData` and don't reference DOCX constructs directly (except math).
3. **The team caught this during planning, not during implementation.** Wei's challenge session surfaced every leakage point before any PPTX code was written.

## What Went Wrong

### Finding 1: Architecture docs don't enforce themselves

The architecture doc said "the architecture must not paint us into a corner" and described four PPTX seam points. But no mechanism existed to verify that the implementation honored these seams. `BlockquoteIndentTwips` went into `ResolvedTheme` in Sprint 1 and was never flagged.

**Root cause:** Architecture was treated as a kickoff artifact, not a living constraint. No fitness function, checklist item, or review lens checked for architectural conformance during implementation.

### Finding 2: YAGNI misapplied to planned capabilities

During implementation sprints, using twips directly in `ResolvedTheme` was the simplest thing that worked. Abstracting to format-neutral units would have felt like over-engineering under YAGNI. But PPTX support wasn't speculative — it was architecturally planned from kickoff. The abstraction boundary was the requirement.

**Root cause:** The team lacked a clear principle distinguishing "don't build for hypothetical futures" (YAGNI) from "maintain the abstraction boundary for planned capabilities."

### Finding 3: No architectural conformance lens in code review

The code-review process has three lenses: Vik (simplicity), Tara (testing), Pierrot (security). Plus situational lenses for Ines (operational), migration safety, and API compatibility. None of these check whether changes to shared types in Core introduce format-specific assumptions.

**Root cause:** The review lenses were designed for general software quality, not for this project's specific architectural constraint (format neutrality in Core).

### Finding 4: Architecture doc described intent as fact

Section 9 of the architecture doc states "The theme schema already has a `pptx:` section." The YAML has a commented-out `# pptx:` placeholder. The doc describes aspiration as if it were implemented, and nobody caught the gap because the doc was never validated against the implementation.

**Root cause:** Architecture docs are written once during kickoff and not re-validated at sprint boundaries. There is no "architecture drift check" in the sprint boundary workflow.

## Recurring Issue Check

Checked all 10 prior retros (`docs/retrospectives/`). No prior retro identified architectural conformance or format-specific drift as an issue. This is a **new finding**, not a recurring one.

However, it rhymes with the Sprint 9 finding about the missing Wei debate for ADR-0012 — both are cases where the architecture gate process existed on paper but wasn't fully enforced. Sprint 10 addressed the Wei debate gap specifically, but the broader pattern (architecture intentions vs. implementation reality) was not generalized.

## Architecture Gate Compliance

**N/A** — This retro covers cross-sprint drift, not a specific sprint's gate compliance.

## Actionable Changes

| # | Change | Where | Issue |
|---|--------|-------|-------|
| 1 | Add "Architectural Conformance" as a code-review lens | `code-reviewer.md` agent, `code-review.md` skill, `team-governance.md` | Created |
| 2 | Add Fitness Functions section to ADR template | `docs/adrs/template.md` | Created |
| 3 | Add architecture drift check to sprint boundary | `.claude/commands/sprint-boundary.md` | Created |
| 4 | Add YAGNI-vs-planned-capability guidance to gotchas | `docs/process/gotchas.md` | Created |
| 5 | Add conformance review responsibility to Archie agent | `.claude/agents/archie.md` | Created |
| 6 | Add architectural conformance item to Done Gate | `docs/process/done-gate.md` | Created |
