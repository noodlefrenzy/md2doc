---
agent-notes:
  ctx: "Sprint 8 retrospective — Polish + Ship (partial delivery)"
  deps: [docs/sprints/sprint-8-plan.md, docs/tech-debt.md]
  state: active
  last: "grace@2026-03-13"
---
# Sprint 8 Retrospective — Polish + Ship

**Sprint:** 8
**Date:** 2026-03-13
**Duration:** 1 session (Wave 1 only)
**Sprint Goal:** md2 v1 is reliable, well-documented at the CLI surface, and validated end-to-end.

## Summary

Sprint 8 planned 6 items (P0 + P1) across 2 waves plus 2 stretch items in Wave 3. Only Wave 1 (3 items) was completed. Waves 2 and 3 were not attempted. This is the first sprint in the project's history where planned P0 work was left incomplete.

**Completed (3 of 8 planned):**
- #78 (P0, M) -- fix(cli): Playwright timeout and cancellation support for Mermaid/Math
- #60 (P0, M) -- chore(cli): CLI polish, --help refinement, error messages
- #56 (P0, S) -- feat(emit-docx): DOCX metadata (subject, keywords from front matter)

**Not completed (5 of 8 planned):**
- #57 (P0, L) -- test(e2e): 20-page comprehensive document validation -- NOT STARTED
- #58 (P1, M) -- test(visual): preset visual regression snapshots -- NOT STARTED
- #54 (P1, M) -- feat(cli): md2 doctor diagnostic command -- NOT STARTED
- #34 (P2, M) -- perf: Mermaid/math rendering benchmarks -- NOT STARTED (stretch)
- #55 (P2, M) -- feat(cli): pipeline inspection -- NOT STARTED (stretch)

**Deferred past v1 (as planned):** #59, #53, #52, #51.

**Test count:** 536 to 549 (+13 new tests). Modest growth, consistent with the small scope actually delivered.

## What Went Well

1. **Wave 1 items were clean and well-scoped.** All three Wave 1 items (#78, #60, #56) were independent, correctly sized, and delivered without complications. The Playwright timeout/cancellation fix (#78) was an important reliability improvement -- the CLI can no longer hang indefinitely on browser operations.

2. **CLI is now release-quality for first impressions.** The --help output, error messages, and --cover flag (#60) make a meaningful difference in user experience. This was the right work to prioritize.

3. **DOCX metadata is complete.** With #56 shipping subject and keywords, all standard front matter fields now map to DOCX document properties. This was a small item with high polish-per-effort, exactly as the sprint plan predicted.

4. **No new tech debt incurred.** The delivered items were clean implementations with no shortcuts or deferred cleanup.

## What Didn't Go Well

1. **Only 1 of 3 planned waves executed -- significant under-delivery.** Sprint 8 delivered 3 of 6 P0+P1 items (50% by count, ~40% by estimated effort since #57 was the largest item). The sprint goal -- "md2 v1 is reliable, well-documented at the CLI surface, and validated end-to-end" -- was only partially met. The "validated end-to-end" component (#57, #58) was not started.

2. **One P0 item left incomplete.** #57 (20-page e2e validation) was P0 and sized L. It was planned for Wave 2, which was never started. This is the first time in the project that a P0 item was not delivered. For a sprint themed "Polish + Ship," leaving the primary validation item undone undermines release confidence.

3. **Session capacity was consumed by non-sprint work.** Commit d448665 ("add Prof persona, /whatsit command, and first reference page") introduced new methodology infrastructure that was not on the sprint plan. While this work has long-term value, it consumed session context that could have been applied to Wave 2. Process discipline requires that unplanned work be acknowledged as a trade-off against planned commitments.

4. **No P1 items delivered.** Neither #58 (visual regression) nor #54 (md2 doctor) were started. Both are important for v1 quality and supportability respectively. The 0% P1 delivery rate is a first for this project.

5. **Board items were not tracked.** See Board Health section below.

## Velocity Analysis

| Sprint | Planned | Completed | Completion % | Notes |
|--------|---------|-----------|-------------|-------|
| 1-2 | 14 | 14 | 100% | Foundation sprints |
| 3 | 5 | 5 | 100% | Tables, images, lists |
| 4 | 7 | 7 | 100% | Code blocks, highlights, footnotes |
| 5 | 8 | 8 | 100% | Mermaid, Math, logging |
| 6 | 7 | 7 | 100% | Theme engine |
| 7 | 11 | 11 | 100% | Presets, extraction, document structure |
| **8** | **6 (P0+P1)** | **3** | **50%** | **First under-delivery** |

This sprint breaks a streak of 100% planned delivery across 7 consecutive sprints. The pattern is notable and worth understanding. Prior sprints were single-session executions with aggressive but achievable plans. Sprint 8 appears to have been disrupted by unplanned work consuming context budget.

## Architecture Gate Compliance -- Sprint 8

**ADRs created or modified this sprint:** 0
**Debate tracking artifacts this sprint:** 0

**Assessment:** No items in the sprint plan required the Architecture Gate. The sprint plan explicitly notes this. #55 (pipeline inspection) might have needed an arch gate check, but it was a stretch item and was never reached.

No gaps found. No retroactive ADRs needed.

## Board Health and Status Compliance -- Sprint 8

**FINDING: Sprint 8 items are not on the project board.**

All 8 sprint items (#78, #60, #56, #57, #58, #54, #34, #55) are absent from the GitHub Projects board (project #14). This means:

- No status transitions were recorded for any Sprint 8 item.
- The mandatory In Progress to In Review to Done flow could not be enforced.
- The board does not reflect current project state.

**Root cause:** Items were not added to the board at sprint start. The sprint plan references issue numbers and labels, but the items were never linked to the project board.

**Impact:** This is a process violation. The board is the single source of truth for item status. Without board tracking, we cannot verify that items followed the correct status flow, and we lose visibility into work-in-progress at any point during the sprint.

**Recommendation:** Sprint 9 planning must include a step to verify all sprint items are on the board before work begins. This should be added as a pre-flight check.

## Tech Debt Review

**Last reviewed:** Sprint 7 boundary (2026-03-12)
**New debt incurred this sprint:** 0
**Debt resolved this sprint:** 0

### Open Debt Status

| ID | Description | Open Since | Sprints Open | Status |
|----|-------------|-----------|-------------|--------|
| TD-002 | FrontMatterExtractor in wrong assembly/namespace | Sprint 1 | 7 sprints | User-deferred post-v1 |
| TD-005 | Md2.Emit.Docx references Md2.Parsing for AdmonitionBlock | Sprint 4 | 4 sprints | User-deferred post-v1 |
| TD-006 | BrowserManager null-check not synchronized | Sprint 5 | 3 sprints | User-deferred post-v1 |

### Escalation Note

All three items exceed the 3-sprint escalation threshold. Per Grace's escalation authority, these would normally be forced to P0 for the next sprint. However, all three have been **explicitly deferred post-v1 by user decision**, which is the one override that supersedes escalation authority. The deferral is documented and accepted.

No new escalation action required, but this should be re-evaluated if v1 release slips or if v2 planning begins.

## Lessons Learned

1. **Silent scope reduction is the most dangerous process failure.** Preview (Feature Area 8, 5 P1 acceptance criteria) was demoted from core Sprint 8 scope to "Deferred Past v1" without human approval. The rationale given ("power-user feature," "approximation by design") contradicted the discovery notes, the v1 implementation plan, and multiple prior discussions where preview was confirmed as core. No agent caught this because Wei only activates for architecture decisions, Cam only activates for new work, and Grace didn't diff the sprint plan against the v1 plan. **Corrective action:** Scope Reduction Gate added to team-governance.md (§ Scope Reduction Gate). New persona triggers for feature scope reduction and sprint planning. Issue #82.

2. **Unplanned work must be budgeted explicitly.** The Prof persona and /whatsit infrastructure work was valuable but competed with sprint commitments for session context. If methodology work is needed, it should either be on the sprint plan or the sprint plan should be right-sized to accommodate it.

3. **100% velocity is not a guarantee.** Seven consecutive sprints of 100% delivery created an expectation that may have influenced sprint sizing. Sprint 8 planned 6 P0+P1 items plus 2 stretch, which is comparable to prior sprints, but this assumed all session context would go to sprint work.

4. **Board compliance requires active enforcement at sprint start.** Items were on the repo (labels, issues) but not on the project board. These are separate systems and both need to be populated.

5. **Wave 2 dependency on Wave 1 was correct but irrelevant.** The plan correctly noted that #57 benefits from #78 (Playwright stability). This dependency was satisfied when #78 shipped in Wave 1, but Wave 2 was never started regardless.

## Action Items

| # | Action | Owner | Priority |
|---|--------|-------|----------|
| 1 | Add all Sprint 9 items to GitHub Projects board at sprint start | Grace | P0 -- blocking |
| 2 | Add board pre-flight check to sprint planning template | Grace | P0 -- process |
| 3 | Carry #57 (20-page e2e validation) forward as P0 for Sprint 9 | Pat + Grace | P0 |
| 4 | Carry #58 (visual regression) and #54 (md2 doctor) forward for Sprint 9 triage | Pat | P1 |
| 5 | Establish rule: unplanned methodology work during a sprint requires explicit acknowledgment of impact on sprint commitments | Grace | Process note |
| 6 | Review Sprint 7 retro action items #1 and #2 (inline image and Mermaid caption integration tests) -- these were not addressed in Sprint 8 | Grace | P2 -- carry forward |
| 7 | Scope Reduction Gate added to team-governance.md — Wei challenges demotions, Cam validates human intent, Grace diffs plan, human approves | Grace | **DONE** — #82 |
| 8 | Restore Preview (#51, #52) to Sprint 9 as P1 core scope — was incorrectly deferred | Pat + Grace | P0 — blocking for sprint plan |

## Process Improvement Gate

The following items must be resolved before Sprint 9 begins:

- [ ] **Action #1:** All Sprint 9 items added to project board and verified via `gh project item-list`.
- [ ] **Action #2:** Board pre-flight check documented (verify items exist on board, not just in repo issues).
- [ ] **Action #3:** #57 confirmed as P0 carry-forward in Sprint 9 plan.

## Metrics

| Metric | Value |
|--------|-------|
| Items planned (P0+P1) | 6 |
| Items completed | 3 |
| Items not started | 3 (P0: 1, P1: 2) |
| Stretch items completed | 0 of 2 |
| Completion rate (P0+P1) | 50% |
| Tests added | +13 |
| Total tests | 549 |
| Tech debt resolved | 0 |
| Tech debt added | 0 |
| Board items tracked | 0 of 8 (process violation) |

## Operational Baseline Audit — Sprint 8

### Ines: Operational Concerns

| Concern | Status | Finding |
|---------|--------|---------|
| Logging | Foundation | Microsoft.Extensions.Logging wired, --verbose/--debug work. Minor gap: LogWarning/LogError channels unused — all warnings go directly to stderr. |
| Error UX | Below Foundation | `ThemeParseException` extends `Exception` directly, not `Md2Exception` — bypasses user-facing error handling in `ConvertCommand`. Exit code semantics (1 vs 2) inverted from spec but internally consistent. |
| Debug support | Below Foundation | `TransformContext.Warnings` populated by Mermaid/Math transforms but never consumed — silent rendering failures are invisible to users at any verbosity level. |
| Config health | Foundation | Theme YAML validated at load, template safety checked, CLI overrides validated, clear error messages with line numbers. |
| Graceful degradation | Foundation | Timeouts on all Playwright operations, CancellationToken threaded through pipeline, browser-not-installed produces actionable guidance. |

**Result: 2 concerns Below Foundation (Error UX, Debug support).** This does not trigger the blocking gate (requires 3+), but both should be addressed in Sprint 9.

**Action items from Ines:**
1. [P1] Surface `TransformContext.Warnings` to stderr after `pipeline.Transform()` in `ConvertCommand`
2. [P1] Fix `ThemeParseException` to extend `Md2Exception` (or add dedicated catch in `ConvertCommand`)
3. [P2] Reconcile exit code semantics (code vs. documented spec)
4. [P3] Route warning output through `ILogger` instead of raw `Console.Error`

### Diego: README 5-Minute Test

- **Result:** Pass
- **Execution-verified:** `dotnet build`, CLI `--help`, basic conversion, preset flag, style override, verbose mode, `theme resolve` — all passed
- **Read-verified:** `dotnet test`, macOS `open` command
- **Issues found:** None blocking. P3: README Usage section missing `--template`, `--toc`, `--toc-depth`, `--cover`, `--debug` flags added in recent sprints.
