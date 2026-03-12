---
agent-notes:
  ctx: "Sprint 7 retrospective — Presets, Extraction, Document Structure"
  deps: [docs/sprints/sprint-7-plan.md]
  state: active
  last: "grace@2026-03-12"
---
# Sprint 7 Retrospective — Presets, Extraction & Document Structure

**Sprint:** 7
**Date:** 2026-03-12
**Duration:** 1 session (3 waves)
**Sprint Goal:** Complete theme presets, extraction pipeline, CLI commands, and document structure features (TOC, cover page, cross-references, page headers)

## Summary

Sprint 7 delivered all 11 planned items across 3 waves:
- 2 tech debt/fix items (#76, #77)
- 1 small feature (#33 — image captions)
- 4 theme items (#43, #44, #45, #46)
- 4 document structure items (#47, #48, #49, #50)

Test count: 459 → 536 (+77 new tests). All green.

## What Went Well

1. **Full wave execution without blocking.** All 3 waves completed in a single session. Wave dependencies (presets → extraction → CLI; TOC → cover → bookmarks → headers) were correctly ordered.

2. **Code review caught a critical OOXML bug.** Background code review identified that inline images (`![alt](url)` inside paragraphs) were producing invalid Paragraph-inside-Paragraph XML. This was fixed immediately (#49 commit), preventing corrupt documents in production.

3. **Theme engine is now complete end-to-end.** Users can: list presets, apply presets, extract styles from DOCX templates, validate custom themes, and use CLI overrides. The full 4-layer cascade is wired and validated.

4. **TD-004 resolved cleanly.** The ExtractInlineText duplication (3 copies across TableBuilder, ListBuilder, DocxAstVisitor) was consolidated into a shared InlineTextExtractor helper without behavioral changes.

5. **Document structure features work together.** Cover page + TOC + cross-references + headers are composable — `--toc --cover` produces a professional document with suppressed headers on the cover page.

## What Could Be Better

1. **Mermaid caption leak.** The hardcoded alt text "Mermaid diagram" started rendering as a visible caption when #33 was implemented. This interaction wasn't caught by tests because Mermaid rendering tests don't exercise the full emitter pipeline. Caught by code review, not TDD.

2. **Inline image nesting bug was not caught by existing tests.** The Paragraph-inside-Paragraph issue only manifests when images appear inside inline contexts (not block-level). Unit tests for ImageBuilder only tested block-level images. Integration coverage gap.

3. **Large session context pressure.** 11 items + code reviews in one session pushed context limits. The boundary process had to start in a continuation session.

## Action Items

| # | Action | Owner | Priority |
|---|--------|-------|----------|
| 1 | Add integration test for inline images inside paragraphs | Tara | P2 — next sprint if capacity |
| 2 | Add integration test for Mermaid diagram caption suppression | Tara | P2 — next sprint if capacity |
| 3 | Consider wave-size limits (max 4 items per wave) to leave context budget for boundary | Grace | Process note |

## Architecture Gate Compliance — Sprint 7

**ADRs created or modified this sprint:** 0
**Debate tracking artifacts this sprint:** 0

**Assessment:** No new architectural decisions were made during Sprint 7. All work was covered by existing ADRs:
- ADR-0009 (YAML Theme DSL) — presets, extraction, validation
- ADR-0004 (Open XML SDK) — TOC, cover page, bookmarks, headers
- ADR-0011 (System.CommandLine) — CLI commands

No gaps found. No retroactive ADRs needed.

## Board Compliance — Sprint 7

**Items audited:** 11 (all Sprint 7 issues)
**Status flow compliance:** 11/11 items followed In Progress → In Review → Done flow.
**Items that skipped statuses:** 0

All items transitioned through the required statuses before closing.

## Operational Baseline Audit — Sprint 7

### Ines: Operational Concerns

| Concern | Status | Finding |
|---------|--------|---------|
| Logging | Foundation | Microsoft.Extensions.Logging wired (#71, Sprint 5). --debug flag works. New features (TOC, cover, bookmarks) don't add log calls but operate within existing logged pipeline. |
| Error UX | Foundation | ThemeValidator produces actionable error messages with property paths. CLI commands handle FileNotFoundException with user-friendly messages. |
| Debug support | Foundation | Theme cascade has `md2 theme resolve` for debugging. Verbose logging available via --debug. |
| Config health | Foundation | Theme YAML validated at load time. Invalid presets rejected with clear messages. |
| Graceful degradation | Foundation | External calls (Playwright) have timeout support (#78 tracks remaining gaps). Non-Mermaid/Math documents work without browser. |

### Diego: README 5-Minute Test

- **Result:** Pass (execution-verified)
- **Issues found:** None — `dotnet build` and `dotnet test` both pass. CLI `--help` shows new theme subcommands and --toc/--toc-depth options.

### Gate

0 concerns below Foundation level. Gate passes.

## Metrics

| Metric | Value |
|--------|-------|
| Items planned | 11 |
| Items completed | 11 |
| Items deferred | 0 |
| Tests added | +77 |
| Total tests | 536 |
| Tech debt resolved | 1 (TD-004) |
| Tech debt added | 0 |
| Code review findings fixed | 2 (inline image nesting, Mermaid caption) |
