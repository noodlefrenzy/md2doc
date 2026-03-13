---
agent-notes:
  ctx: "Sprint 10 retrospective — Process Cleanup + Release Readiness"
  deps: [docs/sprints/sprint-10-plan.md, docs/tech-debt.md]
  state: active
  last: "grace@2026-03-13"
---
# Sprint 10 Retrospective — Process Cleanup + Release Readiness

**Sprint:** 10
**Date:** 2026-03-13
**Duration:** 1 session (single wave)
**Sprint Goal:** All process debt cleared, v1 release-ready.

## Summary

Sprint 10 delivered both P0 items in a single wave. This was a focused process sprint — no new features, no architectural changes. The sprint cleared the remaining process-improvement debt from Sprint 9's retro.

**Completed (2 of 2 planned — 100% delivery):**

### P0 — Must Do (2/2)
- #85 (S) — test: integration tests for inline images and Mermaid caption edge cases — 7 tests
- #83 (S) — process: retroactive Wei debate for ADR-0012 (HTML preview server)

### P2 — Stretch
- Pre-release audit (dead code pass + dependency health) — to be evaluated during boundary

## What Went Well

1. **Fast turnaround on process debt.** Both items completed in a single session. The process-improvement escalation mechanism (3-sprint overdue → P0) worked — #85 was 3 sprints overdue and got done immediately when escalated.

2. **Test quality.** The 7 integration tests for #85 exercise the full pipeline (parse → transform → emit → inspect DOCX structure). The AST mutation pattern for simulating Mermaid rendering without Playwright is clean and reusable.

3. **Retroactive debate value.** The Wei debate for ADR-0012 documented trade-offs (TcpListener vs Kestrel, Playwright for browser launch, polling vs SSE) that were made implicitly during Sprint 9. Even retroactively, the debate artifact provides value for future maintainers.

## What Could Be Better

1. **Sprint 10 was almost trivially small.** Two S-sized items in a "sprint" is arguably not a sprint — it's a cleanup task. The sprint boundary overhead (retro, backlog sweep, gate, audit) is significant relative to the work done. For future process-only sprints, consider batching them with the previous sprint's boundary.

2. **No stretch items attempted.** The pre-release audit (dead code pass + dependency health) was listed as stretch but not picked up. This will be evaluated during the boundary's periodic passes (Step 5).

## Process Observations

- **Process-improvement escalation works.** #85 was created in Sprint 7, deferred in Sprints 8 and 9, and auto-escalated to P0 in Sprint 10. The mechanism is functioning as designed.
- **Architecture Gate retroactive compliance is expensive.** Creating a debate artifact retroactively (#83) provides documentation value but doesn't catch design issues early. The Sprint 9 retro correctly identified this as a gap.

## Architecture Gate Compliance

**ADRs created this sprint:** 0
**ADRs modified this sprint:** 0 (ADR-0012 exists from Sprint 9, no changes in Sprint 10)
**Debate tracking artifacts created this sprint:** 1 (2026-03-13-preview-debate.md for ADR-0012)
**Architecture Gate compliance:** 1/1 — ADR-0012 now has a retroactive Wei debate tracked.
**Unrecorded architectural decisions:** 0 — Sprint 10 was test/process only, no architectural changes.

## Board Compliance

To be filled during Step 1b.

## Metrics

| Metric | Sprint 9 | Sprint 10 |
|--------|----------|-----------|
| Items planned | 8 | 2 |
| Items completed | 8 (100%) | 2 (100%) |
| Items deferred | 0 | 0 |
| Commits | 14 | 2 |
| New tests | ~75 | 7 |
| Process violations | 1 (missing Wei debate) | 0 |

## Board Compliance — Sprint 10

**Board compliance:** 2/2 items followed the full status flow (In Progress → In Review → Done).
No status transition violations.

## Operational Baseline Audit — Sprint 10

### Ines: Operational Concerns

| Concern | Status | Finding |
|---------|--------|---------|
| Logging | Foundation | ILogger throughout pipeline, --debug/--verbose wired, per-phase timing |
| Error UX | Foundation | Md2Exception hierarchy with UserMessage/Message split, consistent catch-wrap-rethrow |
| Debug support | Foundation | Transform names in logs, cascade trace, md2 doctor, --debug stack traces |
| Config health | Foundation | ThemeValidator with Error/Warning severity, eager validation, template safety checks |
| Graceful degradation | Foundation | 30s Playwright timeouts, missing-Chromium detection, partial-failure degradation to warnings |

**Minor items (not blockers):** PreviewSession lacks launch timeout for Playwright. PreviewCommand generic catch doesn't mention --debug. SyntaxHighlightAnnotator/DocxEmitter have no logging.

### Diego: README 5-Minute Test

- **Result:** Pass
- **Execution-verified:** build, help, convert, preset, style overrides, verbose, quiet, theme resolve, doctor, version
- **Read-verified:** preview, Mermaid rendering, math rendering (require display/Chromium)
- **Issues found:**
  - P2: README project structure missing Md2.Preview
  - P3: Undocumented CLI commands (doctor, preview, theme extract/validate/list)
  - P3: No sample .md file in repo for the example command

### Pierrot: Pre-Release Dependency Health

- **Security:** No CVEs in NuGet vulnerability DB. Playwright bundles older Chromium. Markdig 2 major versions behind.
- **Health:** System.CommandLine still on beta4 (stable 2.0.4 available). TextMateSharp.Grammars version mismatch (1.0.69 vs 2.0.3 core).
- **SBOM:** Needs updating (missing logging deps, version pins stale, CVE table needs update).
- **Verdict:** No release blockers. Dependency upgrades recommended pre-release but not veto-worthy.
