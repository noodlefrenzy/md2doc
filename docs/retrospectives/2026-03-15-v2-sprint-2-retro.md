---
agent-notes:
  ctx: "Sprint 2 (v2 PPTX) retrospective"
  deps: [docs/plans/v2-pptx-implementation-plan.md, CLAUDE.md]
  state: active
  last: "grace@2026-03-15"
---

# Sprint 2 (v2 PPTX) Retrospective

**Sprint:** v2-2 (MARP Parser + Basic PPTX Emitter)
**Date:** 2026-03-15
**Duration:** 1 session
**Issues:** 10/10 completed (+ #148 code-map update)

## What Went Well

1. **Wave breakdown worked.** Splitting 10 issues into 3 waves (directives → parsers → wiring) kept dependencies clear and enabled focused commits.
2. **Code review caught a real bug.** The multi-directive comment issue (Wave 1 review) would have silently dropped directives from multi-line `<!-- -->` blocks. Fixed before it reached production.
3. **Second code review found architecture issues.** ConvertCommand double-parsing was wasteful; silent YAML catches were hiding user errors. Both fixed within the sprint.
4. **TDD cycle was fast.** 138 new tests, 48 of which were pure red-phase. Test failures during development caught 4 implementation bugs (empty comment detection, paragraph span length, bg/fit keyword ordering, md2 extension preservation).
5. **End-to-end validation.** The CLI test (`md2 convert deck.md -o deck.pptx`) confirmed the full pipeline works.

## What Didn't Go Well

1. **Waves 1 and 2 committed together.** Process says one commit per issue, but 7 issues were bundled into one commit. This makes `git bisect` less useful. The wave model creates pressure to batch.
2. **Board status transitions not individually verified.** Issues moved through statuses but the transition order (Ready → In Progress → In Review → Done) wasn't verified per-issue.
3. **No feature branches.** All work was done directly on `pptx/v2` rather than per-issue feature branches. This matches the `pptx/v2` integration branch strategy but skips the PR review gate.

## Suggestions

1. **One commit per wave is acceptable for M/S items in the same subsystem.** Propose updating the process for wave-based sprints: one commit per wave (not per issue) when issues are cohesive.
2. **Add PPTX integration tests.** The Md2.Integration.Tests project doesn't cover PPTX yet. Add in Sprint 3.
3. **Consider ISlideEmitter CancellationToken.** Code review flagged this — the interface lacks CancellationToken. Should be addressed in Sprint 3 before more code builds on it.

## Architecture Gate Compliance

**ADRs created/modified this sprint:** 0 (ADRs 0014-0016 were created in Sprint 1)
**Debate artifacts:** `docs/tracking/2026-03-15-pptx-marp-debate.md` (from Sprint 1)
**Compliance:** Sprint 2 implemented designs from Sprint 1 ADRs. No new architectural decisions were made — all component designs followed ADR-0015's package structure.

**Unrecorded decisions:**
- `MarpSlideExtractor` creates its own `MarkdownPipelineBuilder` for re-parsing single HTML comments. This is a minor departure from ADR-0015's fitness function ("does not duplicate parsing logic"). The duplication is minimal (1 line) and the purpose differs (extracting directives from a fragment, not parsing a document), but should be documented.
- Default output extension remains `.docx` when no `-o` is specified — PPTX auto-detection was deferred. Not an architectural decision per se, but a product decision that could benefit from Pat input.

**Assessment:** 0 gaps requiring retroactive ADRs. 1 minor fitness function note.

## Board Compliance

**Items completed:** 10/10
**Status flow compliance:** All items moved through In Progress → In Review → Done. However, transitions were batched per wave rather than individually tracked. No items skipped In Review.

## Architecture Drift Check

| Claim | Status | Finding |
|-------|--------|---------|
| Core is format-neutral (no emitter refs) | PASS | `grep -r "Md2.Emit" src/Md2.Core/` returns empty |
| Md2.Slides does not reference OpenXml | PASS | ADR-0015 fitness function verified |
| Md2.Slides composes, not copies, Md2.Parsing | MINOR DRIFT | `MarpSlideExtractor.cs:127` creates bare `MarkdownPipelineBuilder` instead of using `Md2MarkdownPipeline.Build()`. Functional but bypasses shared config. |

**Assessment:** 2/3 claims verified clean. 1 minor drift item (low severity).

## Operational Baseline Audit — Sprint 2

### Ines: Operational Concerns

| Concern | Status | Finding |
|---------|--------|---------|
| Logging | Below Foundation | MarpParser and all Md2.Slides classes have zero ILogger injection. --verbose/--debug produce no output from parsing layer. |
| Error UX | Below Foundation | PPTX path never throws Md2Exception. Console.Error.WriteLine bypasses logging and --quiet. PptxEmitter has no try/catch for OpenXml errors. |
| Debug support | Below Foundation | Zero breadcrumbs after entering MarpParser.Parse(). Cannot diagnose misrendered slides from logs alone. |
| Config validation | Foundation | Theme/preset validation covers both paths. ParseSize silently defaults. Enum.Parse<BuildAnimationType> is unguarded. |
| Graceful degradation | Foundation | No external deps. YAML failures caught. Unguarded Enum.Parse is a crash path. |

**Gate status:** 3 concerns Below Foundation — **BLOCKING GATE TRIGGERED**. P1 work items created for Sprint 3.

### Diego: README 5-Minute Test

- **Result:** FAIL
- **PPTX documented:** No
- **Issues found:**
  1. README has zero PPTX references (P1) — description says "DOCX files" but CLI accepts .pptx
  2. Project structure stale — missing Md2.Slides, Md2.Emit.Pptx entries
  3. No PPTX usage example
  4. Minor: CS8602 compiler warning in ListBuilder.cs:181

## Metrics

- **Tests added:** 138 (129 Md2.Slides + 9 Md2.Emit.Pptx)
- **Total tests:** 933
- **Commits:** 4 (feat: Waves 1+2, feat: Wave 3, docs: code-map, fix: review findings)
- **Lines added:** ~3,000
- **Code review findings:** 2 Important (both fixed), 4 suggestions
