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
| Logging | Below Foundation | No logging framework configured. CLI accepts --verbose flag but no implementation. TD-003 tracks this. |
| Error UX | Foundation | CLI errors go to stderr with clean messages. Exit codes defined (0/1/2). FrontMatterParseException has line numbers. ImageBuilder returns placeholder for missing files. |
| Debug support | Below Foundation | No structured logging. Developers must use debugger. |
| Config health | N/A | No config files yet (theme engine is Sprint 6). |
| Graceful degradation | Foundation | FrontMatterExtractor handles missing/malformed YAML. ImageBuilder handles missing image files with placeholder text. SmartTypographyTransform skips code spans safely. |

### Diego: README 5-Minute Test

- **Result:** Pass (partial)
- **Execution-verified:** `dotnet build`, `dotnet test`, `dotnet run --project src/Md2.Cli -- --help`, `dotnet run --project src/Md2.Cli -- input.md -o output.docx` (produces valid 3.4KB DOCX)
- **Issues found:** README predates Sprint 3 additions. Quick-start is functional but doesn't mention table/list/image capabilities.

### Gate Assessment

2 concerns below Foundation (Logging, Debug support). Same as Sprint 1-2. Project is at Sprint 3, so the 3+ threshold gate applies. However, only 2 concerns are below Foundation (threshold is 3), so the gate **passes**. TD-003 (logging) remains tracked for Sprint 8.
