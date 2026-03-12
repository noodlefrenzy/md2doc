---
agent-notes:
  ctx: "Sprint 5 retrospective — Mermaid + Math rendering"
  deps: [CLAUDE.md, docs/sprints/sprint-5-plan.md]
  state: active
  last: "grace@2026-03-12"
---
# Sprint 5 Retrospective

**Sprint:** 5 — Math + Mermaid + Operational Baseline
**Date:** 2026-03-12
**Issues completed:** 8 of 12 (4 deferred by user decision)
**Test count:** 339 (63 Core + 43 Parsing + 123 Emit.Docx + 27 Integration + 37 Highlight + 26 Diagrams + 20 Math)
**Commits:** 9 (7 feature + 2 chore/handoff)

## What Went Well

1. **P0 operational baseline resolved.** Logging framework (#71) and consistent error handling (#72) addressed the Sprint 4 blocking gate. 27 log calls across 5 key files. Md2Exception hierarchy established.
2. **Two new projects introduced cleanly.** Md2.Diagrams (Mermaid rendering) and Md2.Math (LaTeX-to-OMML) both follow established patterns — IAstTransform, embedded resources, clear separation.
3. **Playwright shared infrastructure works well.** BrowserManager provides shared lifecycle for both Mermaid and Math. Lazy-init pattern keeps single-use CLI fast while supporting batch rendering.
4. **Content-hash caching for Mermaid diagrams.** SHA256 + version salt avoids redundant rendering across invocations. Good performance for iterative document editing.
5. **End-to-end wiring completed.** MermaidDiagramRenderer and MathBlockAnnotator transforms + DocxAstVisitor handlers tested and reviewed. User validated output DOCX quality.
6. **Wave-based execution maintained.** 4 waves planned, 3 executed. User chose to skip Wave 4 and move to Sprint 6 — clean decision with clear carryover list.

## What Didn't Go Well

1. **Markdig MathInline quirk.** Single-line `$$...$$` becomes `MathInline` with `DelimiterCount=2`, not `MathBlock`. Required runtime type-checking order adjustment in DocxAstVisitor (MathBlock before FencedCodeBlock). Not well-documented in Markdig.
2. **BrowserManager race condition (I1).** `GetBrowserAsync` null-check not synchronized. Low risk in current single-threaded CLI but technically a bug. Carried as known issue.
3. **README outdated.** Project structure in README doesn't include Md2.Math and Md2.Diagrams projects or their test projects. Feature list doesn't mention syntax highlighting, Mermaid diagrams, or math rendering.
4. **#32 (MathBuilder) scope ambiguity.** The end-to-end wiring commit implemented OMML insertion in DocxAstVisitor directly, largely absorbing #32's scope. Issue left in Ready status rather than explicitly closed or refined.

## Metrics

| Metric | Value |
|--------|-------|
| Issues planned | 12 |
| Issues completed | 8 |
| Issues deferred (user decision) | 4 (#32, #33, #34, #69) |
| Velocity | 8/12 (67% — adjusted: 8/8 user-scoped = 100%) |
| New test files | ~12 |
| New tests added | ~59 (26 Diagrams + 20 Math + 13 integration/other) |
| New projects | 4 (Md2.Diagrams, Md2.Diagrams.Tests, Md2.Math, Md2.Math.Tests) |
| Build status | Green (1 warning — nullable CS8604 in ListBuilderTests) |

## Architecture Gate Compliance

**ADRs used this sprint:** 2 — ADR-0006 (LaTeX-to-OMML converter, pre-existing), ADR-0008 (Playwright Mermaid rendering, pre-existing).

**ADRs created/modified this sprint:** 0 new ADRs.

**Debate tracking artifacts for Sprint 5:** 0. All relevant ADRs were debated in Sprint 1 discovery phase.

**Cross-reference audit:**
- ADR-0006 (LaTeX-to-OMML): Debate recorded in `docs/tracking/archive/sprint-4/2026-03-11-md2doc-adr-debate.md`. ✅
- ADR-0008 (Playwright-Mermaid): Debate recorded in same artifact. ✅

**Unrecorded architectural decisions:**
1. **KaTeX page reuse pattern** — `LatexToOmmlConverter` keeps a single browser page alive across calls via lazy init, reusing it for all math expressions. This is a performance/resource management decision that affects memory profile. Low risk — follows same pattern as MermaidRenderer.
2. **DiagramCache content-hash with version salt** — Cache invalidation strategy baked into DiagramCache. Follows standard content-addressable caching. Low risk.

**Architecture Gate compliance:** 2/2 pre-existing ADRs had Wei debates tracked. 2 implementation decisions made without ADRs (both low-risk extensions of established patterns). No retroactive ADRs needed.

## Board Compliance

**Board status:** 34 Done (30 prior + 4 Sprint 5), 4 Ready (Sprint 5 deferred), 29 Backlog (Sprint 6-8)
**Sprint 5 completed items (8):** #27, #28, #29, #30, #31, #71, #72, #73

**Status flow audit:**
- #27, #28, #29, #30, #31: Followed In Progress → In Review → Done. ✅
- #71, #72: Followed In Progress → In Review → Done. ✅
- #73: Closed immediately (fix already in prior boundary commit). Minor flow deviation — acceptable for zero-effort closures.

**Board compliance:** 7/8 items followed the full status flow. 1 item (#73) had an expedited close (pre-existing fix). ✅

## Process Improvements Identified

1. **P-004: README must be updated when new projects are added.** Sprint 5 added Md2.Diagrams and Md2.Math but README project structure and features list were not updated. This is a recurring pattern (Sprint 4 also had README issues).
2. **P-005: Resolve scope overlap for partially-absorbed issues.** #32 (MathBuilder) was largely implemented by the end-to-end wiring commit but left in Ready status. Issues should be explicitly closed (with rationale) or refined to remaining scope at sprint boundary.

## Tech Debt Update

| ID | Status Change | Notes |
|----|--------------|-------|
| TD-001 | **ESCALATION: 3-sprint threshold** | Incurred Sprint 2, now at Sprint 5 boundary. Auto-P0 for Sprint 6. Resolution: ThemeCascadeResolver (#36) replaces hardcoded ResolvedTheme. |
| TD-003 | **RESOLVED** | Logging framework wired by #71. |
| I1 | **New (known issue)** | BrowserManager.GetBrowserAsync race condition — null-check not synchronized. Low risk in CLI context. |

## Operational Baseline Audit — Sprint 5

### Ines: Operational Concerns

| Concern | Status | Finding |
|---------|--------|---------|
| Logging | **Foundation** ✅ | ILogger configured via Microsoft.Extensions.Logging. --debug flag enables full diagnostics. 27 log calls across pipeline, diagrams, math. INFO level on --debug shows transform ordering, file paths, phase timing. |
| Error UX | **Foundation** ✅ | Md2Exception base type with Md2ConversionException. Top-level catch in ConvertCommand produces clean "Error: ..." messages. Exit code 2 for internal errors, 1 for user errors. |
| CLI Contract | **Foundation** ✅ | stdout for output path, stderr for diagnostics. --help, --quiet, --verbose, --debug, --version all work. Exit codes: 0/1/2. |
| Config UX | **Foundation** ✅ | CLI-only config validated by System.CommandLine. No env vars needed. Adequate for current scope. |
| Graceful Degradation | **Foundation** ✅ | ImageBuilder returns placeholder for missing images. MermaidDiagramRenderer and MathBlockAnnotator degrade gracefully without Chromium (code-span fallback for math, skip for Mermaid). BrowserManager logs warnings. |
| Documentation Bit-Rot | **Below** ⚠️ | README project structure missing Md2.Math, Md2.Diagrams and their test projects. Features list doesn't mention syntax highlighting, Mermaid, or math. |
| Error Pattern Consistency | **Foundation** ✅ | Md2Exception hierarchy established. ConvertCommand catch block is single pattern. Library code no longer writes to Console.Error directly. |
| Debug Support | **Foundation** ✅ | --debug flag shows full pipeline phase execution, transform names+order, file I/O paths. Sufficient to diagnose failures from output alone. |
| Idempotency | **Foundation** ✅ | File conversion is naturally idempotent — same input produces same output. Cache uses content-hash. |
| External Process Spawn | **Foundation** ✅ | Playwright managed via BrowserManager with explicit lifecycle. No raw subprocess spawning. |

**Below-Foundation count: 1** (Documentation Bit-Rot)
**Gate status: PASS** — Only 1 concern below Foundation (threshold is 3 for blocking). P1 work item created for README update.

### Diego: README 5-Minute Test

- **Result:** Partial Pass
- **Execution-verified:** `dotnet build` ✅, `dotnet run --project src/Md2.Cli -- input.md -o output.docx` ✅, `dotnet test` (339 pass) ✅, `--help` ✅, `--version` ✅, `--debug` ✅
- **Issues found:**
  1. **Project structure outdated (P1):** Missing `Md2.Math/`, `Md2.Diagrams/`, `Md2.Math.Tests/`, `Md2.Diagrams.Tests/` from the listing.
  2. **Features list incomplete:** No mention of syntax highlighting, Mermaid diagram rendering, or math/equation support — three of the tool's flagship capabilities.
  3. **Features list mentions "Theme engine" but it doesn't exist yet** — aspirational content in README.
