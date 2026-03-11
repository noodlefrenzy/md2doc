---
agent-notes: { ctx: "Sprint 1+2 retrospective", deps: [docs/plans/v1-implementation-plan.md], state: active, last: "grace@2026-03-11" }
---

# Retrospective — Sprints 1 & 2

**Date:** 2026-03-11
**Sprint scope:** Issues 1-14 (parsing foundation, core pipeline, basic DOCX emission, CLI skeleton)
**Outcome:** All 14 issues completed and closed. 145 tests passing.

## What Went Well

1. **Velocity was high.** 14 issues across 2 sprints completed in a single session. The TDD workflow (Tara → Sato) produced clean, testable code.
2. **Architecture held up.** The Parse → Transform → Emit pipeline design from ADR-0005 worked smoothly. No rework needed.
3. **Test coverage is solid.** 145 tests covering parsing, AST extensions, pipeline orchestration, DOCX emission, inline formatting, page layout, document properties, and E2E integration.
4. **Board tracking worked.** Issues moved through statuses cleanly (Ready → In Progress → Done).

## What Didn't Go Well

1. **Sprint boundary was not run.** Sprints 1 and 2 completed without running `/sprint-boundary`. The human had to prompt for it. This is a process violation — the boundary should trigger automatically when sprint items complete.
2. **In Review status was skipped.** Issues went directly from In Progress → Done, bypassing the mandatory In Review status. Code review (Vik + Tara + Pierrot) was invoked but status wasn't transitioned to In Review first.
3. **One commit per sprint, not one per issue.** The process calls for one commit per issue. Sprints 1 and 2 each had a single bulk commit covering all issues. This makes `git bisect` less useful and doesn't match the conventional commits convention properly.
4. **.gitignore was missing .NET entries.** `bin/` and `obj/` directories were almost committed because the template .gitignore didn't include .NET-specific patterns. Caught before push but should have been addressed during project scaffold.
5. **Circular dependency design needed real-time resolution.** The Md2.Core ↔ Md2.Parsing namespace/assembly split required placing FrontMatterExtractor in a non-standard location (Core assembly, Parsing namespace). This works but is unusual and flagged by diagnostics.

## Architecture Gate Compliance

### ADRs This Sprint
No new ADRs were created during Sprints 1-2. All 9 ADRs (0003-0011) were created during the pre-sprint architecture phase and have corresponding debate tracking in `docs/tracking/2026-03-11-md2doc-adr-debate.md`.

**Compliance: 9/9 ADRs have debate tracking. No gaps.**

### Unrecorded Architectural Decisions
1. **ParserOptions placement in Md2.Parsing** — Decision to put ParserOptions in the Md2.Parsing assembly instead of Md2.Core to avoid circular dependencies. No ADR written. Low risk — this is an assembly layout decision, not a design choice.
2. **FrontMatterExtractor in Core assembly with Parsing namespace** — Same circular dependency resolution. Documented via code comment but no ADR. Low risk.
3. **Shouldly chosen as assertion library** — Test strategy doc mentions it but no formal ADR. Very low risk.

**Assessment:** No ADR-worthy decisions were made without tracking. The assembly placement decisions are implementation details, not architecture.

## Process Improvements Identified

| # | Finding | Severity | Action |
|---|---------|----------|--------|
| PI-1 | Sprint boundary not triggered automatically | Medium | Add to CLAUDE.md: Grace must trigger sprint boundary when all sprint items are Done |
| PI-2 | In Review status skipped | Medium | Enforce In Review transition before Done |
| PI-3 | Bulk commits instead of per-issue commits | Low | Follow one-commit-per-issue discipline starting Sprint 3 |
| PI-4 | .gitignore missing .NET patterns | Low | Fixed. No further action. |

## Operational Baseline Audit — Sprints 1 & 2

### Ines: Operational Concerns

| Concern | Status | Finding |
|---------|--------|---------|
| Logging | Below Foundation | No logging framework configured. CLI has no --verbose output implementation beyond flag acceptance. |
| Error UX | Foundation | CLI errors go to stderr with clean messages. Exit codes defined (0/1/2). FrontMatterParseException has line numbers. |
| Debug support | Below Foundation | No structured logging. Developers must use debugger. |
| Config health | N/A | No config files yet (theme engine is Sprint 6). |
| Graceful degradation | Foundation | FrontMatterExtractor handles missing/malformed YAML gracefully. |

### Diego: README 5-Minute Test

- **Result:** Not fully testable — README predates implementation. Quick-start instructions reference `md2` command which is now buildable but README was not updated to reflect actual build/run steps.
- **Issues found:** README needs update to reflect actual project structure and build commands.

### Gate Assessment
2 concerns below Foundation (Logging, Debug support). Project is at Sprint 2, so the 3+ threshold gate doesn't apply yet. These will be tracked and reviewed at next boundary.
