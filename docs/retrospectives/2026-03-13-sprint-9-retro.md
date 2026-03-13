---
agent-notes:
  ctx: "Sprint 9 retrospective — Preview + Validation + Operational Fixes"
  deps: [docs/sprints/sprint-9-plan.md, docs/tech-debt.md]
  state: active
  last: "grace@2026-03-13"
---
# Sprint 9 Retrospective — Preview + Validation + Operational Fixes

**Sprint:** 9
**Date:** 2026-03-13
**Duration:** 2 sessions (Waves 1-3)
**Sprint Goal:** md2 v1 has a working preview mode, end-to-end validation, and no Below Foundation operational gaps.

## Summary

Sprint 9 delivered all 8 planned items across 3 waves, including both stretch items. This is a full recovery from Sprint 8's 50% delivery rate and the strongest sprint since Sprint 7. The wave planning approach worked exactly as designed — each wave was self-contained and committable.

**Completed (8 of 8 planned — 100% delivery):**

### P0 — Must Do (3/3)
- #57 (L) — test(e2e): comprehensive 20-page document validation — 38 structural checks
- #80 (S-M) — fix(cli): surface TransformContext.Warnings to stderr
- #81 (S) — fix(themes): ThemeParseException extends Md2Exception

### P1 — Should Do (3/3)
- #51 (L) — feat(preview): HTML preview server with hot-reload via Playwright
- #52 (M) — feat(cli): md2 preview command with hot-reload
- #54 (M) — feat(cli): md2 doctor diagnostic command

### P2 — Stretch (2/2)
- #58 (M) — test(visual): preset visual regression snapshots (18 tests, 5 JSON snapshots)
- #34 (M) — perf: Mermaid and math rendering benchmarks (10 diagrams < 15s, 25 expressions < 10s)

**Deferred: 0 items.**

## What Went Well

1. **Wave planning validated.** Breaking 8 items into 3 waves (4 + 2 + 2) with clear dependencies created natural commit/handoff boundaries. Each wave was independently valuable.

2. **100% delivery including stretch.** The sprint plan sized conservatively (6 P0+P1 items as the "realistic ceiling" from Sprint 8 retro). This left room to attempt stretch items.

3. **Code review findings addressed inline.** Every feature commit (#51, #52, #54, #57) received code-reviewer review and had a follow-up fix commit. This added ~30% commit overhead but caught: CSS injection vulnerability, torn-read race condition, silent exception swallowing, double markdown parsing, missing error handlers.

4. **Board compliance restored.** All 8 items tracked on the project board with correct status transitions. This addresses Sprint 8 retro Action Item #1.

5. **Architecture Gate followed.** ADR-0012 written before #51 implementation, as the sprint plan required.

6. **Test count growth.** Added 53 tests this sprint: 30 (Preview), 18 (Snapshots), 2 (Benchmarks), 3 (Preview CLI). Total: ~595.

## What Needs Improvement

1. **No formal Wei debate for ADR-0012.** The ADR evaluates 4 options but no debate tracking artifact exists. The Architecture Gate requires Wei as devil's advocate, but this wasn't formally recorded. The decision quality appears sound (the code review validated the architecture), but the process was incomplete.

2. **Sprint 7 retro action items still unaddressed.** Items #1 and #2 (inline image and Mermaid caption integration tests) were flagged in Sprint 7, carried in Sprint 8's retro, and remain unaddressed in Sprint 9. These are now 3 sprints overdue.

3. **Context consumed across 2 sessions.** Wave 1 consumed the first session's full context. The second session resumed successfully via `/resume` but required context reconstruction from the handoff file. The handoff was stale (written after Sprint 8, not updated for Sprint 9 Wave 1).

4. **Handoff staleness risk.** The handoff file from the end of Sprint 8 was not updated after Wave 1 completed. The `/resume` detected the staleness and recovered, but this relied on git state verification rather than an up-to-date handoff.

## Architecture Gate Compliance

- **ADRs created this sprint:** 1 (ADR-0012: HTML Preview Server)
- **Wei debates tracked:** 0/1 — ADR-0012 lacks a formal debate artifact
- **Architectural decisions without ADRs:** 0 — sprint plan correctly identified only #51 as needing the gate
- **Assessment:** Process partially followed. The ADR was written before implementation (correct), but the Wei debate step was skipped. Since this is a test-oriented sprint with one architectural decision, the gap is minor but should not become a pattern.

## Board Compliance

- **Items with full status flow:** 8/8 — all items transitioned through In Progress → In Review → Done
- **Status violations:** 0
- **Assessment:** Full compliance. Board tracking was restored as a priority after Sprint 8 violations.

## Metrics

| Metric | Sprint 7 | Sprint 8 | Sprint 9 |
|--------|----------|----------|----------|
| Items planned | 8 | 8 | 8 |
| Items completed | 8 | 3 | 8 |
| Delivery rate | 100% | 38% | 100% |
| Commits | 10 | 6 | 12 |
| Review-fix commits | 2 | 0 | 3 |
| Tests added | ~100 | ~30 | ~53 |
| Total tests | ~475 | ~558 | ~595 |

## Benchmark Results (Baseline)

| Benchmark | Result | Target | Status |
|-----------|--------|--------|--------|
| 10 Mermaid diagrams | 10.5s | < 15s | Pass (30% margin) |
| 25 math expressions | 1.3s | < 10s | Pass (87% margin) |

## Action Items

1. **Create Wei debate artifact retroactively for ADR-0012.** Not blocking, but record the decision rationale for the audit trail.
2. **Address Sprint 7 retro items #1 and #2** (inline image + Mermaid caption integration tests). These are now 3 sprints overdue. Create issues and schedule for Sprint 10.
3. **Update handoff after each wave.** When a session completes a wave but not the sprint, run `/handoff` before ending the session. This prevents stale handoffs.
4. **Continue wave planning.** The 3-wave structure (fast-fixes → features → stretch) worked well. Adopt as the default sprint structure.

## Operational Baseline Audit — Sprint 9

### Ines: Operational Concerns

| Concern | Status | Finding |
|---------|--------|---------|
| Logging | Foundation | Microsoft.Extensions.Logging wired. `--verbose`/`--debug` flags on ConvertCommand. PreviewCommand lacks verbosity flags (noted in code review, non-blocking). |
| Error UX | Foundation | Md2Exception hierarchy with UserMessage. ThemeParseException fixed (#81). Transform warnings surfaced (#80). Preview command handles unknown presets and cancellation. |
| Debug support | Foundation | `--debug` flag exposes full diagnostics. `md2 doctor` validates 5 prerequisites. |
| Config health | Foundation | Theme validation at startup. Invalid preset/theme produces actionable error messages. |
| Graceful degradation | Foundation | Playwright timeouts configured (#78). File watcher debounces and swallows IOException for mid-write files. Preview server handles client disconnects. |

### Diego: README 5-Minute Test

- **Result:** Pass
- **Method:** Execution-verified (`dotnet run --project src/Md2.Cli -- --help`)
- **Issues found:** None — CLI boots and shows help text.

### Gate

0 concerns below Foundation. Gate passes.

## Process-Improvement Issues

| # | Title | Status |
|---|-------|--------|
| #83 | Retroactive Wei debate for ADR-0012 | Open — schedule for Sprint 10 |
| #84 | Update handoff after each wave | Resolved — added to gotchas.md |
| #85 | Integration tests for inline images + Mermaid captions | Open — schedule for Sprint 10 |
