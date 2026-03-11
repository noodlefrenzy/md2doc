---
agent-notes: { ctx: "Sprint 3 retrospective", deps: [docs/plans/v1-implementation-plan.md], state: active, last: "grace@2026-03-11" }
---

# Retrospective — Sprint 3

**Date:** 2026-03-11
**Sprint scope:** Issues 15-19 (table auto-sizing, table styling, images, lists, smart typography)
**Outcome:** All 5 issues completed and closed. 184 tests passing (up from 145).

## What Went Well

1. **Per-issue commit discipline followed.** Each of the 4 implementation issues (16-19) got its own commit, plus a separate integration commit. This follows PI-3 from the Sprint 1-2 retro.
2. **Status transitions followed.** All issues went through In Progress → In Review → Done. PI-2 from Sprint 1-2 retro was addressed.
3. **Test coverage grew substantially.** 39 new tests across 4 test files (TableStylingTests, ImageBuilderTests, ListBuilderTests, SmartTypographyTransformTests).
4. **Builder pattern scales well.** TableBuilder, ListBuilder, ImageBuilder all follow the same pattern — constructor takes ParagraphBuilder, public Build method returns OpenXml elements. This is a clean, extensible architecture.
5. **Prototype gate worked.** Issue 15 (TableBuilder prototype) validated the auto-sizing algorithm before Issue 16 added styling. The gate caught column width constraint bugs early.

## What Didn't Go Well

1. **Visitor dispatch not wired.** The Sato agent created all 4 builders and updated the DocxAstVisitor constructor to hold references, but didn't wire the `VisitBlock` dispatch. Tables, lists, and images were dead code until manually fixed. This means the builders passed unit tests but wouldn't have worked in E2E without the integration fix.
2. **ImageBuilder used wrong API.** `ImagePartType` is a static class in Open XML SDK 3.x, not an enum. The generated code used it as a return type, causing CS0722. Required manual fix to use content type strings instead.
3. **Code duplication across builders.** `ExtractInlineText` is duplicated in TableBuilder, ListBuilder, and DocxAstVisitor (3 copies). Should be extracted to a shared utility.
4. **DocxEmitter didn't pass theme to visitor.** The emitter was still using the backward-compat constructor (`new DocxAstVisitor(paragraphBuilder, mainPart)` instead of the 3-arg version). This meant table builds would use default theme, not the actual resolved theme.

## Architecture Gate Compliance

### ADRs This Sprint
No new ADRs were created during Sprint 3.

### Unrecorded Architectural Decisions
1. **Builder pattern for DOCX element generation** — TableBuilder, ListBuilder, ImageBuilder all follow the same pattern (constructor injection of ParagraphBuilder, public Build method). This is an implementation pattern, not an architectural decision. No ADR needed.
2. **SmartTypographyTransform at Order=50** — Placing smart typography after front matter extraction (Order=10) but before other transforms. This is a minor ordering decision, not architecture.
3. **OpenXml Numbering definitions created per-list** — ListBuilder creates a new AbstractNum + NumberingInstance for each list rather than reusing. This is an implementation detail.

**Compliance: No ADR-worthy decisions were made without tracking. All Sprint 3 work followed patterns established in Sprints 1-2.**

## Board Compliance

**Board compliance: 5/5 items followed the full status flow (In Progress → In Review → Done).**
No status transitions were skipped. Process improvements PI-1 and PI-2 from Sprint 1-2 retro were successfully adopted.

## Process Improvements Identified

| # | Finding | Severity | Action |
|---|---------|----------|--------|
| PI-5 | ExtractInlineText duplicated across 3 files | Low | Refactor to shared utility in a future sprint |
| PI-6 | Agent-generated code had API compatibility error (ImagePartType) | Medium | Verify Open XML SDK 3.x API before accepting agent output |
| PI-7 | Visitor dispatch not wired by agent despite builder references being added | Medium | Verify E2E integration after agent work, not just unit tests |

## Operational Baseline Audit — Sprint 3

### Ines: Operational Concerns

| Concern | Status | Finding |
|---------|--------|---------|
| Logging framework | Below | No ILogger, no Microsoft.Extensions.Logging, no structured logging anywhere in src/. All output is ad-hoc Console.Error in ConvertCommand. |
| --verbose flag | Foundation | Flag exists and prints input/output paths + stack traces on error. But only 2 breadcrumbs for entire conversion — no phase timing, no transform names, no AST stats. |
| --quiet flag | Below | Declared and parsed but **never read** in ExecuteAsync. Dead code. |
| Pipeline stage logging | Below | ConversionPipeline.Parse/Transform/Emit produce no log output. Cannot tell which stage failed or how long each took. |
| Error pattern (top-level) | Foundation | Single catch in ConvertCommand prints "Error: {message}" to stderr. Exit code 1 for errors, 2 for file-not-found. Reasonable starting point. |
| Error pattern (FrontMatter) | Foundation | Custom FrontMatterParseException with line numbers. Good. |
| Error pattern (ImageBuilder) | Below | GetImageDimensions has bare catch that silently swallows all exceptions, returns default 6x4 size. Silent failure. |
| Error pattern (unhandled blocks) | Below | VisitBlock returns empty for unrecognized types (FencedCodeBlock, QuoteBlock, ThematicBreakBlock, HtmlBlock silently dropped). Content lost with no warning. |
| Config validation | N/A | CLI tool with no config files. System.CommandLine validates args. |
| Debug support | Below | Only input path + error message available. No phase ID, no timing, no document stats. Impossible to diagnose beyond "it failed." |
| Graceful degradation (images) | Foundation | ImageBuilder checks File.Exists, returns styled placeholder. Good. |
| Graceful degradation (URLs) | Foundation | VisitLink catches UriFormatException, falls back to plain text. Silent but non-destructive. |
| Graceful degradation (cancellation) | Below | No CancellationToken threading. EmitAsync accepts no cancellation. |

**Summary:** 7 Foundation, 7 Below, 2 N/A

**Top Ines recommendations:**
1. Integrate Microsoft.Extensions.Logging with ILogger injection
2. Fix dead --quiet flag (wire it or remove it)
3. Warn on silently dropped content in VisitBlock
4. Stop swallowing exceptions in ImageBuilder.GetImageDimensions
5. Add CancellationToken to pipeline

### Diego: README 5-Minute Test

- **Result:** FAIL (tool works; README does not document it)
- **Execution-verified:** `dotnet build` (pass), `dotnet test` (184/184 pass), `dotnet run --project src/Md2.Cli -- --help` (pass), `dotnet run --project src/Md2.Cli -- --version` (pass), end-to-end convert to .docx (pass, 3.4KB valid DOCX with styles, numbering, footer)
- **Issues found:**
  1. **[P1] README is the vteam-hybrid template README**, not md2doc documentation. Zero build/run/usage instructions. A newcomer cannot get started.
  2. **[P2] No sample markdown file** in the repo for quick verification.
  3. **[P3] Output path derivation undocumented** (input.md → input.docx rule not explained).
- **Positive:** The tool itself works flawlessly. Build, CLI, E2E, all 184 tests pass on first try with zero warnings.

### Gate Assessment

**7 concerns below Foundation** (logging framework, --quiet dead code, pipeline logging, ImageBuilder error swallowing, unhandled block dropping, debug support, cancellation). This **exceeds the 3-concern threshold** and the project is past Sprint 2.

**Gate: BLOCKING.** Grace creates P1 work items for below-Foundation concerns for Sprint 4 inclusion.

However, many of these concerns will be naturally resolved by Sprint 4 work (code blocks, blockquotes, thematic breaks add VisitBlock handlers) and Sprint 8 (logging, TD-003). The blocking items that need immediate attention:
1. Dead --quiet flag (trivial fix)
2. ImageBuilder silent exception swallowing (trivial fix)
3. README has no md2doc content (P1 per Diego)

These 3 items are added to Sprint 4 as P1 work items.
