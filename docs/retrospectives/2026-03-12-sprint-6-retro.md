---
agent-notes:
  ctx: "Sprint 6 retrospective — Theme Engine"
  deps: [docs/sprints/sprint-6-plan.md]
  state: active
  last: "grace@2026-03-12"
---
# Sprint 6 Retrospective — Theme Engine

**Sprint:** 6
**Date:** 2026-03-12
**Duration:** 1 session (Waves 1-4 executed across 2 session halves)
**Sprint Goal:** Build the theme engine (YAML parsing, cascade resolution, validation, CLI integration)

## Summary

Sprint 6 delivered the complete theme engine. All 10 planned items completed:
- 8 feature issues (#35-#42)
- 1 README fix (#74)
- 1 process evaluation (#69)

TD-001 (hardcoded ResolvedTheme) was resolved — the primary P0 goal.

## What Went Well

1. **TD-001 resolution.** The 3-sprint-old tech debt item is finally resolved. `ResolvedTheme.CreateDefault()` is no longer called in production code. The cascade resolver is the single source of truth.

2. **Wave structure worked.** Breaking the sprint into 4 waves with clear dependencies (Foundation → Cascade → Validation+CLI → Verbose+Process) prevented blocking and kept focus.

3. **Code review findings were actionable.** Both review rounds (Waves 1-2 and Wave 3) produced Important-severity findings that were promptly fixed: negative margin validation, uint overflow, invariant culture parsing, data-driven refactoring.

4. **Test count growth.** From 339 tests at sprint start to 458 tests — a 35% increase. Md2.Themes.Tests went from 0 to 119 tests.

5. **Process evaluation (#69) was efficient.** A thorough cross-boundary analysis concluded quickly: no new abstractions project needed, Md2.Core suffices. TD-005 (AdmonitionBlock coupling) was confirmed as the one violation to fix post-v1.

## What Could Be Improved

1. **Sprint 6 items weren't on the board initially.** Issues #39-#42 existed as GitHub issues but hadn't been added to the project board. The session had to add them manually before starting work. Sprint planning should ensure all items are on the board.

2. **No unit tests for CLI command wiring.** The `ParseStyleOverrides` method and `Execute` method in ThemeResolveCommand are internal and testable, but have no tests. The formatter is tested, but the CLI orchestration layer is not. This is a coverage gap.

3. **Style override registry is verbose.** The data-driven dictionary approach (from code review refactoring) is more maintainable than the switch statement but still has 60+ entries with significant duplication. A reflection-based or attribute-based approach could reduce this further.

4. **Template extraction not implemented.** The `--template` flag is wired in but template style extraction (DocxStyleExtractor) hasn't been built yet. The flag validates the file and runs safety checks, but doesn't extract styles. This is expected (it's issue #44), but the user might expect it to work now.

## Action Items

| # | Action | Owner | Priority |
|---|--------|-------|----------|
| 1 | Ensure all sprint items are on the board before sprint starts | Grace | Process |
| 2 | Add CLI command integration tests for ParseStyleOverrides | Tara | Next sprint if CLI work continues |
| 3 | Create backlog item for moving AdmonitionBlock to Md2.Core.Ast | Grace | Backlog |

## Architecture Gate Compliance

**ADRs referenced this sprint:**
- ADR-0009 (YAML Theme DSL): Covered #35, #36, #38, #39, #40, #41, #42. Pre-existing from Sprint 5 planning.
- ADR-0010 (IRM Protected Templates): Covered #37. Pre-existing from Sprint 5 planning.

**New ADRs created:** 0

**Debate artifacts:** ADR-0009 and ADR-0010 debates were tracked in Sprint 4 (archived at `docs/tracking/archive/sprint-4/`). No new architectural decisions were made this sprint — all work followed existing ADRs.

**Unrecorded architectural decisions:** None identified. Sprint 6 was pure implementation against existing ADR designs.

**Architecture Gate compliance: 2/2 ADRs had prior Wei debates. 0 architectural decisions made without ADRs.**

## Board Compliance

**Status transition compliance:** All 10 items followed the In Progress → In Review → Done flow. #42 had an accelerated review (small change, low risk) but still passed through In Review status.

**Board compliance: 10/10 items followed the full status flow. 0 items skipped statuses.**

## Metrics

| Metric | Value |
|--------|-------|
| Issues completed | 10 |
| Issues carried forward | 0 |
| Commits | 13 (including review fixes) |
| Tests added | 119 (all in Md2.Themes.Tests) |
| Total tests | 458 |
| Code review rounds | 2 (Waves 1-2 batch, Wave 3) |
| Code review findings fixed | 7 Important, 8 Suggestions |
| Tech debt resolved | 1 (TD-001) |
| Tech debt incurred | 0 |
| ADRs created | 0 |
| Process improvements created | 0 |

## Operational Baseline Audit — Sprint 6

### Ines: Operational Concerns

| Concern | Status | Finding |
|---------|--------|---------|
| Logging coverage | Foundation | Microsoft.Extensions.Logging wired with three tiers (quiet/verbose/debug). 20+ log statements across 5 files. |
| Error pattern consistency | Foundation | Clean two-tier model: Md2Exception with UserMessage for user-facing, debug stack traces. Exit codes 0/1/2 consistent. |
| Debug support | Foundation | --debug shows full stack traces. Timing breakdown logged. Theme cascade trace printed in verbose mode. |
| Config validation | Below | ThemeValidator exists but is never called during convert flow. Invalid theme YAML produces confusing errors at emit time. Created #77. |
| Graceful degradation | Below | BrowserManager lacks timeouts on Playwright launch. No cancellation support. Missing Chromium gives opaque error. Created #78. |

**Gate:** 2/5 concerns below Foundation (threshold is 3). Gate passes.

### Diego: README 5-Minute Test

- **Result:** Pass
- **Issues found:**
  1. README missing new CLI options (--preset, --theme, --template, --style, theme resolve) — fixed in this boundary
  2. README missing Md2.Themes project in structure — fixed in this boundary
  3. Build warning CS8604 in ListBuilderTests.cs (pre-existing, informational)
- **Verification:** Steps 2-5 execution-verified. Step 1 read-verified.
- **No P1 defects found.**
