---
agent-notes:
  ctx: "v2 PPTX implementation plan, sprint breakdown, issue list"
  deps: [docs/adrs/0014-slide-document-ir.md, docs/adrs/0015-marp-parser-architecture.md, docs/adrs/0016-unified-theme-pptx-extension.md, docs/tracking/2026-03-15-pptx-marp-architecture.md]
  state: active
  last: "grace@2026-03-15"
  key: ["3 waves across 6 sprints", "pptx/v2 integration branch", "2 required spikes before Sprint 2", "TDD: Tara tests first, Sato implements"]
---

# md2 v2 -- PPTX Implementation Plan

**Author:** Grace (sprint tracker / coordinator)
**Date:** 2026-03-15
**Status:** Active -- ready for Sprint 1 kickoff
**Predecessor:** `docs/plans/acceptance-criteria-v1.md` (v1), ADRs 0014-0016, discovery/architecture tracking

---

## Plan Overview

**Goal:** Add PPTX output from MARP-styled Markdown to md2. Output must be edit-ready, demonstrably better than Claude conversion, and use the shared theme DSL.

**Sprint count:** 6 sprints across 3 implementation waves
**Sprint model:** Each sprint is a focused work session. Ends with working, tested code on `pptx/v2` branch.
**TDD workflow:** Tara writes failing tests first. Sato makes them pass. Vik + Tara + Pierrot review.
**Branch strategy:** `pptx/v2` integration branch off `main`. Feature branches off `pptx/v2`. Periodic `main` → `pptx/v2` merges. Final PR to `main` when v2.0 is complete.

### Wave-to-Sprint Mapping

| Wave | Sprints | Focus | Key Deliverable |
|------|---------|-------|-----------------|
| **Wave 1: Foundation** | 1, 2 | Spikes, SlideDocument IR, MARP parser, basic PPTX emission | Working `md2 convert deck.md -o deck.pptx` for simple MARP decks |
| **Wave 2: Full Feature** | 3, 4 | Theme integration, slide layouts, speaker notes, headers/footers, tables, code blocks, build animations | Fully-featured PPTX with all v2.0 acceptance criteria |
| **Wave 3: Polish & Ship** | 5, 6 | Mermaid native shapes (flowcharts), charts from data, edge cases, integration tests, documentation | Ship-ready v2.0, PR to main |

### Dependency Graph (Sprint Level)

```
Sprint 1 (Spikes + SlideDocument IR + SlidePipeline)
  |
  v
Sprint 2 (MARP Parser + Basic PPTX Emitter)
  |
  +----------+----------+
  |          |          |
  v          v          v
Sprint 3   Sprint 4   Sprint 5
(Themes,   (Headers,  (Mermaid
 Layouts,   Footers,   Native
 Speaker    Tables,    Shapes,
 Notes)     Code,      Charts)
            Build
            Animations)
  |          |          |
  +----------+----------+
         |
         v
    Sprint 6 (Integration, Docs, Ship)
```

---

## Sprint Breakdown

### Sprint 1: Foundation — IR, Pipeline, Spikes

**Goal:** Validate architectural assumptions and build the core types.

| # | Issue Title | Size | Priority | Notes |
|---|-------------|------|----------|-------|
| 1 | Spike: AST fragment reparenting validation | S | P0 | Required before Sprint 2. Test Markdig node detachment + transform compatibility. |
| 2 | Spike: Fragmented list marker detection in Markdig | S | P0 | Required before Sprint 2. Test `ListBlock.BulletType` per-item preservation. |
| 3 | Create `pptx/v2` integration branch | XS | P0 | Branch setup, CI config. |
| 4 | Implement `SlideDocument` IR types in `Md2.Core/Slides/` | M | P0 | `SlideDocument`, `Slide`, `SlideLayout`, `SlideDirectives`, `PresentationMetadata`, `IDocumentMetadata`. |
| 5 | Implement `SlidePipeline` orchestrator | M | P0 | Parse → MARP extract → Transform → Emit. Parallel to `ConversionPipeline`. |
| 6 | Implement `ISlideEmitter` interface | S | P0 | `EmitAsync(SlideDocument, ResolvedTheme, EmitOptions, Stream)` |

**Sprint 1 exit criteria:** Spike results documented. `SlideDocument` types compile. `SlidePipeline` wires parse → emit with a stub emitter.

### Sprint 2: MARP Parser + Basic PPTX Emission

**Goal:** Parse real MARP decks and produce basic PPTX output.

| # | Issue Title | Size | Priority | Notes |
|---|-------------|------|----------|-------|
| 7 | Implement `MarpParser` top-level (string → SlideDocument) | M | P0 | Orchestrates extraction, splitting, assembly. |
| 8 | Implement `MarpDirectiveExtractor` | M | P0 | Extract directives from `HtmlBlock` comment nodes. |
| 9 | Implement `MarpDirectiveClassifier` | S | P0 | Classify as global, local, or scoped. |
| 10 | Implement `MarpDirectiveCascader` | M | P0 | Apply cascading semantics across slides. |
| 11 | Implement `MarpSlideExtractor` | M | P0 | Split Markdig AST at `ThematicBreakBlock` + `headingDivider`. Handle cross-slide refs. |
| 12 | Implement `MarpImageSyntax` parser | M | P1 | Parse `bg`, `w:`, `h:`, `cover`, `contain`, `fit`, `left:N%`, `right:N%`. |
| 13 | Implement `MarpExtensionParser` (md2 extensions) | S | P1 | Parse `<!-- md2: { ... } -->` YAML payloads. |
| 14 | Implement `SlideLayoutInferrer` | S | P1 | Infer layout from content + class directive. |
| 15 | Basic `PptxEmitter` — slides with text content only | L | P0 | Create `PresentationDocument`, slide masters, text frames. Minimal styling. |
| 16 | Wire PPTX path in CLI (`ConvertCommand`) | S | P0 | Detect `.pptx` extension → `SlidePipeline` + `PptxEmitter`. |

**Sprint 2 exit criteria:** `md2 convert simple-deck.md -o output.pptx` produces a valid PPTX with correct slides, text, and basic formatting. All Marpit v3.x directives parsed.

### Sprint 3: Theme Integration + Slide Layouts + Speaker Notes

**Goal:** Themed PPTX output with proper layouts and presenter notes.

| # | Issue Title | Size | Priority | Notes |
|---|-------------|------|----------|-------|
| 17 | Extend `ThemeDefinition` with `ThemePptxSection` | M | P0 | YAML schema for `pptx:` section including per-format color overrides. |
| 18 | Implement `ResolvedPptxTheme` sub-object | M | P0 | Cascade resolver populates `ResolvedTheme.Pptx`. |
| 19 | Add `pptx:` sections to all existing presets | M | P0 | default, modern, technical, nightowl, etc. |
| 20 | Implement slide master/layout generation from theme | L | P0 | Title, Content, TwoColumn, SectionDivider, Blank layouts. |
| 21 | Implement speaker notes emission | S | P0 | `Slide.SpeakerNotes` → PPTX notes slide. |
| 22 | Implement `<!-- fit -->` heading auto-scale | S | P1 | Auto-shrink text to fit slide width. |
| 23 | MARP `theme:` directive → preset hint mapping | S | P1 | gaia→default, uncover→modern, etc. |
| 24 | `md2 theme resolve --format pptx` support | S | P1 | Show PPTX cascade resolution. |

**Sprint 3 exit criteria:** Themed PPTX with proper slide layouts. Speaker notes visible in presenter view. All presets produce styled output.

### Sprint 4: Headers/Footers, Tables, Code Blocks, Build Animations

**Goal:** All remaining v2.0 content types and slide furniture.

| # | Issue Title | Size | Priority | Notes |
|---|-------------|------|----------|-------|
| 25 | Implement header/footer rendering | M | P0 | MARP `header`/`footer` directives + `<!-- md2: { footer: { left: ..., right: ... } } -->`. |
| 26 | Implement slide number (`paginate`) | S | P0 | `paginate: true/false`, `_paginate` per-slide override. |
| 27 | Implement company logo/image support in headers/footers | M | P0 | Inline images in header/footer markdown + md2 extension for positioned logos. |
| 28 | Implement native PPTX tables | M | P0 | Markdig tables → PPTX table shapes. Theme-styled cells. |
| 29 | Implement syntax-highlighted code blocks | M | P0 | Reuse `SyntaxHighlightAnnotator` tokens → colored PPTX text runs. |
| 30 | Implement build animations (bullet reveal) | M | P0 | `<!-- md2: { build: "bullets" } -->` and fragmented list markers (if spike passed). |
| 31 | Implement background images | M | P1 | `![bg cover](img.jpg)`, `![bg left:30%](img.jpg)`. |
| 32 | Implement inline images | S | P1 | Standard `![alt](img.jpg)` with `w:`/`h:` sizing. |
| 33 | Implement hyperlinks | S | P1 | Clickable links in PPTX text. |
| 34 | Implement blockquotes | S | P1 | Styled quote blocks. |

**Sprint 4 exit criteria:** Full v2.0 acceptance criteria met for content types. Headers/footers with logos. Build animations working.

### Sprint 5: Mermaid Native Shapes + Charts (v2.1 scope)

**Goal:** Native editable diagrams and charts.

| # | Issue Title | Size | Priority | Notes |
|---|-------------|------|----------|-------|
| 35 | Implement Mermaid flowchart → PPTX native shapes | L | P1 | Parse Mermaid graph structure → rectangles, diamonds, connectors. |
| 36 | Implement Mermaid shape styling from theme | S | P1 | Colors, fonts from `ResolvedPptxTheme`. |
| 37 | Implement Mermaid image fallback for complex types | M | P1 | Sequence, Gantt, ER, state, pie → PNG embed (existing path). |
| 38 | Implement `chart` code fence → native PPTX charts | L | P1 | Bar, column, line, pie. Editable in PowerPoint. |
| 39 | Implement chart data format (CSV/YAML) | M | P1 | Define and document the data format. |
| 40 | Implement chart palette from theme | S | P1 | `pptx.chartPalette` colors. |

**Sprint 5 exit criteria:** Flowcharts editable as shapes. Charts editable. Complex diagrams fall back to images.

### Sprint 6: Integration, Documentation, Ship

**Goal:** End-to-end quality, docs, and PR to main.

| # | Issue Title | Size | Priority | Notes |
|---|-------------|------|----------|-------|
| 41 | End-to-end integration tests (comprehensive MARP deck) | L | P0 | Test with real-world MARP presentations. |
| 42 | Cross-application validation (PowerPoint, Google Slides, Impress) | M | P0 | Verify output opens cleanly in all three. |
| 43 | MARP compatibility documentation | M | P0 | Supported/unsupported features, caveats, extension syntax reference. |
| 44 | Update README with PPTX usage | S | P0 | Examples, CLI flags, theme usage. |
| 45 | Update `docs/code-map.md` for v2 packages | S | P0 | Md2.Slides, updated Md2.Emit.Pptx, SlidePipeline. |
| 46 | Quality comparison: md2 vs Claude conversion | M | P0 | Side-by-side comparison demonstrating quality advantage. |
| 47 | Merge `pptx/v2` → `main` PR | S | P0 | Final integration. |

**Sprint 6 exit criteria:** All v2.0 acceptance criteria pass. Documentation complete. Quality comparison demonstrates clear advantage. PR merged to main.

---

## Issue Summary

| Sprint | Issues | Sizes |
|--------|--------|-------|
| 1 | 6 | 2S + 2M + 1XS + 1S |
| 2 | 10 | 2S + 4M + 1L + 1S + 2(S,M) |
| 3 | 8 | 2M + 1L + 3S + 2(M,S) |
| 4 | 10 | 3M + 3S + 2M + 2S |
| 5 | 6 | 2L + 2M + 2S |
| 6 | 7 | 1L + 2M + 3S + 1S |
| **Total** | **47** | |

## Risk Register

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| AST reparenting spike fails | Redesign slide extraction approach | Medium | Fallback to re-parsing from source text (documented in ADR-0014) |
| Fragmented list spike fails | No `*` vs `-` animation distinction | Medium | Custom Markdig extension or defer to v2.1 |
| Mermaid flowchart → shapes is harder than expected | Slip to v2.1 | Medium | Already scoped as v2.1; image fallback always works |
| MARP syntax edge cases | Compatibility gaps | High | Pinned to Marpit v3.x, explicit compatibility scope |
| PPTX XML complexity | Clean output is hard | Medium | Validate with PowerPoint, Google Slides, Impress in Sprint 6 |

## Active Personas Per Sprint

| Sprint | Active Personas |
|--------|----------------|
| 1 | Tara (tests), Sato (implementation), Archie (spike analysis) |
| 2 | Tara, Sato, Vik (code review) |
| 3 | Tara, Sato, Dani (slide layouts/UX), Vik |
| 4 | Tara, Sato, Vik, Pierrot (security — YAML deser, image paths) |
| 5 | Tara, Sato, Vik, Pierrot (shape/chart injection) |
| 6 | Full team — Vik + Tara + Pierrot review, Diego (docs), Grace (board) |
