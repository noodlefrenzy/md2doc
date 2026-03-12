---
agent-notes:
  ctx: "session handoff — Sprint 5 complete, Sprint 6 next"
  deps: [CLAUDE.md, docs/sprints/sprint-5-plan.md, docs/code-map.md]
  state: active
  last: "grace@2026-03-12"
---
# Session Handoff

**Created:** 2026-03-12
**Sprint:** 5 (complete — run `/sprint-boundary` to start Sprint 6)
**Wave:** 3 of 4 executed this session; Wave 4 items deferred or absorbed
**Session summary:** Executed Sprint 5 Wave 3 (Mermaid + Math rendering), then wired all new transforms into the CLI and emitter end-to-end. User reviewed output DOCX and approved moving to Sprint 6.

## What Was Done

### Sprint 5 Wave 3 — Mermaid + Math Rendering (4 issues)
- **#28** (L) MermaidRenderer — Playwright PNG rendering at 2x DPI, SHA256 content-hash caching with version salt, embedded Mermaid JS v11.13.0, error detection via aria-roledescription. Code reviewed (Vik+Tara+Pierrot). 11 tests.
- **#29** (M) MermaidDiagramRenderer — IAstTransform (order 40) for mermaid FencedCodeBlocks. 9 tests.
- **#30** (L) LatexToOmmlConverter — LaTeX → KaTeX (Playwright) → MathML → MML2OMML.xsl → OMML. KaTeX v0.16.38 + MML2OMML.xsl bundled as embedded resources. 12 tests.
- **#31** (M) MathBlockAnnotator — IAstTransform (order 35) for MathBlock + MathInline nodes. 8 tests.

### End-to-End Wiring (not a tracked issue — organic from user request)
- ConvertCommand.cs: registers MermaidDiagramRenderer + MathBlockAnnotator, creates BrowserManager/DiagramCache/LatexToOmmlConverter
- DocxAstVisitor.cs: handles MathBlock (centered OMML paragraph), MathInline (inline OMML), and mermaid-annotated FencedCodeBlocks (PNG via ImageBuilder)
- test-sample.md: added syntax highlighting (C#, Python), Mermaid diagrams (flowchart, sequence), inline math (quadratic formula, Euler's identity), display math (Gaussian integral, matrix, Maxwell's equation)
- User reviewed output DOCX at `~/docs/md2-output/test-sample.docx` — approved quality

### Projects Created
- `Md2.Math` (src + tests) — added to solution

### Docs Updated
- `docs/code-map.md` — updated Md2.Math section and test inventory (339 tests)

## Current State
- **Branch:** main
- **Last commit:** `3f75521` feat: wire Mermaid/Math transforms into CLI and emitter
- **Uncommitted changes:** none
- **Tests:** 339 passing across 8 projects (43 Parsing + 63 Core + 123 Emit.Docx + 27 Integration + 37 Highlight + 26 Diagrams + 20 Math)
- **Board status:** 31 items on board. Sprint 5 items #27-31 Done. #32, #33, #34 still open (Ready). Note: #32 MathBuilder was largely absorbed by the end-to-end wiring commit — the emitter now handles OMML insertion via DocxAstVisitor directly.

## Sprint 5 Remaining Items
The user chose to skip Wave 4 and move to Sprint 6. These items carry forward:
- **#32** (M) MathBuilder — largely done (OMML insertion wired into DocxAstVisitor); may want to close or refine
- **#33** (S) Image captions from alt text — not started
- **#34** (M) Mermaid/math benchmarks — not started
- **#69** (process) Evaluate shared types — not started

## Sprint 6 Scope — Theme Engine
| # | Title | Size |
|---|-------|------|
| 35 | feat(themes): ThemeParser and ThemeDefinition model with YamlDotNet | L |
| 36 | feat(themes): ThemeCascadeResolver with 4-layer merge | L |
| 37 | feat(themes): template safety (IRM detection, .doc rejection, .docm warning, size limit) | M |
| 38 | feat(themes): PresetRegistry with embedded preset YAML loading | M |
| 39 | feat(themes): ThemeValidator with schema checking and line numbers | M |
| 40 | feat(cli): md2 theme resolve command | M |
| 41 | feat(cli): --preset, --theme, --template, --style flags on convert command | S |
| 42 | feat(cli): --verbose shows cascade resolution details and timing | M |

## What To Do Next (in order)
1. Read `docs/code-map.md` to orient
2. Read `docs/product-context.md` for human's product philosophy
3. **Run `/sprint-boundary`** — Sprint 5 is complete. This will:
   - Run the Sprint 5 retrospective
   - Sweep the backlog (carry-forward #32, #33, #34, #69)
   - **TD-001 escalation gate:** hardcoded ResolvedTheme hits 3-sprint threshold — must be addressed or explicitly deferred with user approval
   - Set up Sprint 6 plan with waves
4. Execute Sprint 6 Wave 1 (likely ThemeParser + ThemeDefinition as the foundation)

## Tracking Artifacts
- `docs/tracking/2026-03-11-md2doc-plan.md` — Active, plan phase tracking for v1 implementation
- `docs/tracking/archive/sprint-4/` — Archived discovery and ADR debate artifacts

## Proxy Decisions (Review Required)
None this session.

## Key Context
- **Markdig math types:** `MathBlock` extends `FencedCodeBlock` (must be matched before FencedCodeBlock in switch). Single-line `$$...$$` becomes `MathInline` with `DelimiterCount=2`, not `MathBlock`.
- **Table column splitting:** User noted the wide-table "Priority" column still splits. Preview (#51/#52) is Sprint 8. No fix planned before then unless user prioritizes it.
- **BrowserManager race condition (I1):** Pre-existing — `GetBrowserAsync` null-check not synchronized. Low risk in CLI (single-threaded pipeline) but should be fixed eventually.
- **Version salt not wired:** `DiagramCache` accepts a version salt, `MermaidRenderer.MermaidVersion` is defined, and `ConvertCommand` now passes it. But the cache path is `md2-cache` in temp — shared across invocations, which is good for performance.
- **KaTeX page reuse:** `LatexToOmmlConverter` keeps a single KaTeX page alive across calls via lazy init. Works well for batch conversion.
- **TD-001 at escalation threshold:** Hardcoded `ResolvedTheme.CreateDefault()` everywhere. Sprint 6 (theme engine) is the planned fix. Must be flagged at sprint boundary.
