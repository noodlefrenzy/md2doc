---
agent-notes:
  ctx: "Sprint 4 retrospective — rich content rendering"
  deps: [CLAUDE.md, docs/sprints/sprint-4-plan.md]
  state: active
  last: "grace@2026-03-12"
---
# Sprint 4 Retrospective

**Sprint:** 4 — Rich Content Rendering
**Date:** 2026-03-12
**Issues completed:** 10 (all planned)
**Test count:** 280 (50 Core + 43 Parsing + 123 Emit.Docx + 27 Integration + 37 Highlight)
**Commits:** 11 (10 feature/fix + 1 chore)

## What Went Well

1. **All 10 issues completed.** No carryover, no deferrals. Wave-based execution (4 waves) kept work organized and dependencies clear.
2. **New Md2.Highlight project successfully introduced.** TextMateSharp integration for syntax highlighting with 20+ languages, clean separation via `SyntaxHighlightAnnotator` transform.
3. **TDD workflow maintained consistently.** Every implementation issue had failing tests written first. Total of 82 new tests across Emit.Docx.Tests and Highlight.Tests.
4. **TestHelper pattern worked well.** Shared `TestHelper.CreateVisitor()` enabled focused visitor-level testing without full E2E overhead, reused across 6 test classes.
5. **Bug fixes addressed real quality issues.** #65 (dead --quiet flag) and #66 (silent exception swallowing) improved production-readiness. #67 replaced template README with actual documentation.

## What Didn't Go Well

1. **TextMateSharp API discovery was painful.** No public documentation — had to use reflection to discover that `Theme.Match()` takes `IList<string>`, that `ThemeTrieElementRule` uses fields not properties, and that `IToken.EndIndex` exists. Multiple compile-fix cycles.
2. **Namespace conflicts between Markdig and OpenXml.** Both have `Footnote` and `Table` types. Required using-aliases (`MdFootnote`, `MdTable`). This is a recurring friction point as we add more Markdig extension support.
3. **Shouldly API surprises.** `ShouldNotStartWith` doesn't accept a custom message parameter in Shouldly 4.x, causing a subtle compile error.
4. **Md2.Emit.Docx now references Md2.Parsing.** Added for `AdmonitionBlock` type access. This is a coupling decision that should have had an ADR but was accepted pragmatically.

## Metrics

| Metric | Value |
|--------|-------|
| Issues planned | 10 |
| Issues completed | 10 |
| Velocity | 10/10 (100%) |
| New test files | 8 |
| New tests added | ~82 |
| New projects | 2 (Md2.Highlight, Md2.Highlight.Tests) |
| Build status | Green (0 failures) |

## Architecture Gate Compliance

**ADRs created/modified this sprint:** 1 — ADR-0007 (TextMateSharp syntax highlighting), created during Sprint 1 architecture phase.

**Debate tracking artifacts for Sprint 4:** 0 new debate artifacts. The ADR-0007 debate was recorded in `docs/tracking/2026-03-11-md2doc-adr-debate.md` (Sprint 1 discovery phase).

**Cross-reference audit:**
- ADR-0007 (TextMateSharp): Has corresponding debate in `2026-03-11-md2doc-adr-debate.md`. ✅

**Unrecorded architectural decisions:**
1. **Md2.Emit.Docx → Md2.Parsing project reference** — Added to access `AdmonitionBlock` type. This is a cross-assembly coupling decision that was made without an ADR. Wei could have challenged whether a shared abstractions package or interface would be cleaner.
2. **CodeBlockBuilder as separate class vs. visitor method** — Extracted builder pattern for code blocks (like TableBuilder, ListBuilder) but no ADR for the pattern decision. Low risk since it follows the established builder pattern from Sprint 3.

**Architecture Gate compliance:** 1/1 ADRs had Wei debates tracked. 2 architectural decisions were made without ADRs (one low-risk pattern extension, one moderate-risk coupling decision).

**Action items:**
- Consider retroactive ADR for the Emit.Docx → Parsing dependency direction (schedule for Sprint 5 if coupling grows).

## Board Compliance

**Board status:** 26 Done, 4 Backlog (future sprints)
**Sprint 4 items (10 total):** All 10 reached Done status.

**Status flow audit:** All Sprint 4 items followed In Progress → In Review → Done flow. No items skipped statuses.

**Board compliance:** 10/10 items followed the full status flow. 0 items skipped statuses. ✅

## Process Improvements Identified

1. **P-001: Document TextMateSharp API gotchas** — Create a `docs/gotchas/textmatesharp.md` with the field-vs-property, Match signature, and IToken.EndIndex findings. Saves future debugging time.
2. **P-002: Consider shared types for Markdig extensions** — The Emit.Docx → Parsing dependency for AdmonitionBlock could become a pattern issue as more custom block types are added. Evaluate whether a shared abstractions project is warranted.
3. **P-003: Add Shouldly API notes to gotchas** — Document which Shouldly assertion methods don't accept custom messages to avoid compile surprises.

## Tech Debt Incurred

| ID | Description | Category | Notes |
|----|-------------|----------|-------|
| TD-005 | Md2.Emit.Docx references Md2.Parsing for AdmonitionBlock type | Architecture coupling | May need shared abstractions if more custom types cross this boundary |

## Operational Baseline Audit — Sprint 4

### Ines: Operational Concerns

| Concern | Status | Finding |
|---------|--------|---------|
| Logging | **Below** | No logging framework. All diagnostics are raw `Console.Error.WriteLine`. `--verbose` gates only two messages. Pipeline/transforms/emitter produce zero diagnostic output. |
| Error UX | **Below** | No error module. Top-level catch prints raw `ex.Message`. Internal .NET exceptions surface directly to users. Only one custom exception (`FrontMatterParseException`). |
| CLI Contract | Foundation | stdout/stderr separation correct. `--help`, `--quiet` work. Exit codes functional but semantics inverted vs. convention. |
| Config UX | Foundation | No env vars/config files. CLI args validated by System.CommandLine. Adequate for current scope. |
| Graceful Degradation | Foundation | Filesystem-only I/O. ImageBuilder returns placeholder for missing images. CodeTokenizer has timeout. Reasonable for local tool. |
| Error Pattern Consistency | **Below** | Four catch blocks, four different patterns. Library code (`ImageBuilder`) writes directly to `Console.Error` — layering violation. |
| Debug Support | **Below** | Cannot diagnose failures from output alone. No visibility into pipeline phase, AST node, or source line. Unknown blocks silently produce nothing. |

**Below-Foundation count: 4** (Logging, Error UX, Error Pattern Consistency, Debug Support)
**Gate status: BLOCKING** — 4 concerns below Foundation, project past Sprint 2.

### Diego: README 5-Minute Test

- **Result:** Fail (minor — documentation accuracy, not functional)
- **Issues found:**
  1. Project Structure lists 3 `src/` directories that don't exist yet (Md2.Themes, Md2.Math, Md2.Diagrams)
  2. Missing 2 existing test projects from `tests/` listing (Md2.Core.Tests, Md2.Highlight.Tests)
- **Execution-verified:** build, convert, test (280 pass), verbose, quiet
- **Read-verified:** pipe example, front matter input shape

### Gate Assessment

Per Step 5b gate rules: 4 applicable concerns below Foundation AND project past Sprint 2 → **BLOCKING**. P1 work items created for below-Foundation concerns.
