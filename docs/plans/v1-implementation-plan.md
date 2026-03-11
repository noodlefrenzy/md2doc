---
agent-notes:
  ctx: "v1 implementation plan, sprint breakdown, issue list"
  deps: [docs/plans/acceptance-criteria-v1.md, docs/architecture.md, docs/code-map.md, docs/product-context.md, docs/tracking/2026-03-11-md2doc-adr-debate.md]
  state: active
  last: "grace@2026-03-11"
  key: ["8 sprints across 4 waves", "table auto-sizing gate in Sprint 3", "Math+Mermaid share Playwright dep", "TDD: Tara tests first, Sato implements"]
---

# md2 v1 -- Implementation Plan

**Author:** Grace (sprint tracker / coordinator)
**Date:** 2026-03-11
**Status:** Active -- ready for Sprint 1 kickoff
**Predecessor:** `docs/plans/acceptance-criteria-v1.md` (112 criteria, 4 waves)

---

## Plan Overview

**Total acceptance criteria:** 112 (48 P0, 56 P1, 8 P2)
**Sprint count:** 8 sprints across 4 implementation waves
**Sprint model:** Each sprint is a focused work session (one wave in session terms). Ends with working, tested code. Commit and push at sprint end.
**TDD workflow:** Tara writes failing tests first (as standalone agent). Sato makes them pass. Vik + Tara + Pierrot review.
**Prototype gates:** Table auto-sizing (5-day gate, Sprint 3). Math/Mermaid Playwright integration (Sprint 5).

### Wave-to-Sprint Mapping

| Wave | Sprints | Focus | Key Deliverable |
|------|---------|-------|-----------------|
| **Wave 1: Foundation** | 1, 2 | Parsing, core pipeline, basic DOCX emission, CLI skeleton | Working `md2 input.md -o output.docx` for simple documents |
| **Wave 2: Rich Elements** | 3, 4, 5 | Tables (with prototype gate), code blocks, syntax highlighting, blockquotes, footnotes, admonitions, definition lists, math, Mermaid | All element types emit to DOCX |
| **Wave 3: Style System** | 6, 7 | Theme engine, presets, template cascade, theme management commands, style debugging | Full 4-layer cascade, all 5 presets, theme extract/validate/list/resolve |
| **Wave 4: Polish** | 8 | Preview, multi-file, diagnostics, pipeline inspection, integration testing, P2 items | Ship-ready v1 |

### Dependency Graph (Sprint Level)

```
Sprint 1 (Parsing + Core)
  |
  v
Sprint 2 (Basic DOCX Emission + CLI)
  |
  +------+------+------+
  |      |      |      |
  v      v      v      v
Sprint 3  Sprint 4  Sprint 5
(Tables,  (Code,    (Math,
 Images,  Quotes,   Mermaid,
 Lists)   Footnotes,Preview
          Admon.,   infra)
          DefLists)
  |      |      |
  +------+------+
         |
         v
    Sprint 6 (Theme Engine + Cascade)
         |
         v
    Sprint 7 (Presets + Theme Commands)
         |
         v
    Sprint 8 (Polish, Preview, Multi-file, Doctor, E2E)
```

Sprints 3, 4, and 5 are partially parallelizable (different element types in different .NET projects). Sprint 5 has a hard dependency on Playwright infrastructure that Sprint 4 does not need.

---

## Sprint 1: Parsing Foundation and Core Pipeline

**Wave:** 1 (Foundation)
**Dependencies:** None (first sprint)
**Projects created:** `md2.sln`, `Md2.Core`, `Md2.Parsing`, `Md2.Core.Tests`, `Md2.Parsing.Tests`
**Lead:** Tara (tests) -> Sato (implementation)

### Acceptance Criteria Covered

| ID | Summary | Priority |
|----|---------|----------|
| AC-1.1.1 | CommonMark block elements parsed | P0 |
| AC-1.1.2 | GFM extensions parsed (tables, strikethrough, autolinks, task lists) | P0 |
| AC-1.1.3 | Admonition blocks parsed | P1 |
| AC-1.1.4 | Definition lists parsed | P1 |
| AC-1.1.5 | Generic attributes parsed | P1 |
| AC-1.1.6 | LaTeX math expressions parsed | P1 |
| AC-1.1.7 | Mermaid code blocks identified | P1 |
| AC-1.1.8 | Footnotes parsed | P1 |
| AC-1.1.9 | Nested structures parsed correctly | P0 |
| AC-1.2.1 | YAML front matter extracted | P0 |
| AC-1.2.2 | Standard front matter fields mapped to DocumentMetadata | P0 |
| AC-1.2.3 | Unknown front matter fields preserved | P1 |
| AC-1.2.4 | Malformed YAML front matter error with line number | P0 |

**Also in this sprint:**
- `ConversionPipeline` skeleton (Parse phase only, Transform/Emit stubbed)
- `IAstTransform` interface and `TransformContext`
- `IFormatEmitter` interface and `EmitOptions`
- Typed AST extension methods (`AstDataKeys`, `AstExtensions`) per ADR-0005 debate outcome
- `DocumentMetadata` and `ResolvedTheme` shared types
- `YamlFrontMatterExtractor` transform (order 010)

### Issues

| # | Title | Size | Criteria |
|---|-------|------|----------|
| 1 | feat(parsing): configure Markdig pipeline with CommonMark + GFM extensions | M | AC-1.1.1, AC-1.1.2, AC-1.1.9 |
| 2 | feat(parsing): custom admonition block parser extension | M | AC-1.1.3 |
| 3 | feat(parsing): register definition list, attribute, math, footnote extensions | S | AC-1.1.4, AC-1.1.5, AC-1.1.6, AC-1.1.7, AC-1.1.8 |
| 4 | feat(parsing): YAML front matter extraction with DocumentMetadata mapping | M | AC-1.2.1, AC-1.2.2, AC-1.2.3, AC-1.2.4 |
| 5 | feat(core): pipeline skeleton with IAstTransform, IFormatEmitter, ConversionPipeline | M | (infrastructure) |
| 6 | feat(core): typed AST extension methods and AstDataKeys | S | (ADR-0005 mitigation) |
| 7 | feat(core): YamlFrontMatterExtractor transform | S | AC-1.2.1 (transform side) |

**Sprint 1 total:** 7 issues, ~13 acceptance criteria
**Risk:** Low. Markdig is a well-understood library. The main risk is the custom admonition parser, which has a clear extension API.

---

## Sprint 2: Basic DOCX Emission and CLI Skeleton

**Wave:** 1 (Foundation)
**Dependencies:** Sprint 1 (parsing, core pipeline)
**Projects created:** `Md2.Emit.Docx`, `Md2.Emit.Docx.Tests`, `Md2.Cli`, `Md2.Integration.Tests`
**Lead:** Tara (tests) -> Sato (implementation)

### Acceptance Criteria Covered

| ID | Summary | Priority |
|----|---------|----------|
| AC-3.1.1 | Headings map to Word heading styles | P0 |
| AC-3.1.2 | Heading font/size/color from theme | P0 |
| AC-3.1.3 | Headings in Word Navigation Pane | P0 |
| AC-3.2.1 | Body paragraphs use theme settings | P0 |
| AC-3.2.2 | Bold, italic, strikethrough inline formatting | P0 |
| AC-3.2.3 | Inline code with mono font and background | P0 |
| AC-3.2.4 | Hyperlinks produce clickable links | P0 |
| AC-3.2.5 | Line breaks within paragraphs | P0 |
| AC-3.2.6 | Widow/orphan control | P0 |
| AC-3.5.1 | Page size configurable (A4 default) | P0 |
| AC-3.5.2 | Margins configurable | P0 |
| AC-3.5.3 | Page numbers in footer | P0 |
| AC-9.1.1 | title -> DOCX Title property | P0 |
| AC-9.1.2 | author -> DOCX Creator property | P0 |
| AC-9.1.3 | date -> DOCX Created property | P0 |
| AC-9.1.5 | No front matter -> empty properties | P0 |
| AC-7.1.1 | `md2 input.md -o output.docx` works | P0 |
| AC-7.1.2 | Output file name derived from input when -o omitted | P0 |
| AC-7.1.3 | Exit codes 0, 1, 2 | P0 |
| AC-7.1.4 | Errors to stderr, output path to stdout | P0 |
| AC-7.1.5 | --quiet suppresses warnings | P0 |
| AC-7.1.7 | File not found produces clean error | P0 |
| AC-7.1.8 | --help produces formatted help | P0 |
| AC-7.1.9 | --version outputs version | P0 |
| AC-5.1.3 | Default preset produces clean output with no flags | P0 |
| AC-5.1.5 | Modifying a preset affects only that preset | P0 |

**Also in this sprint:**
- `DocxEmitter : IFormatEmitter` (core visitor for headings, paragraphs, inline runs)
- `DocxAstVisitor` skeleton with `ParagraphBuilder`
- `DocxStyleApplicator` for applying a hardcoded default theme (full theme engine is Sprint 6)
- CLI root command, convert command, global options
- `default.yaml` preset (as a hardcoded in-memory theme until the YAML engine exists)
- First end-to-end integration test: Markdown in, DOCX out, validate with Open XML SDK

### Issues

| # | Title | Size | Criteria |
|---|-------|------|----------|
| 8 | feat(emit-docx): DocxEmitter skeleton with ParagraphBuilder for headings and body text | L | AC-3.1.1, AC-3.1.2, AC-3.1.3, AC-3.2.1, AC-3.2.6 |
| 9 | feat(emit-docx): inline run formatting (bold, italic, strikethrough, inline code, hyperlinks, line breaks) | M | AC-3.2.2, AC-3.2.3, AC-3.2.4, AC-3.2.5 |
| 10 | feat(emit-docx): page layout (page size, margins, page numbers in footer) | M | AC-3.5.1, AC-3.5.2, AC-3.5.3 |
| 11 | feat(emit-docx): document properties from front matter metadata | S | AC-9.1.1, AC-9.1.2, AC-9.1.3, AC-9.1.5 |
| 12 | feat(cli): root command, convert command, global options (--help, --version, -o, -q, -v) | M | AC-7.1.1, AC-7.1.2, AC-7.1.3, AC-7.1.4, AC-7.1.5, AC-7.1.7, AC-7.1.8, AC-7.1.9 |
| 13 | feat(core): hardcoded default ResolvedTheme (placeholder until theme engine) | S | AC-5.1.3, AC-5.1.5 |
| 14 | test(integration): first E2E test -- Markdown in, DOCX out, validate structure | M | (infrastructure, validates Sprint 2 output) |

**Sprint 2 total:** 7 issues, ~26 acceptance criteria
**Sprint 2 exit:** `md2 input.md -o output.docx` produces a valid, styled DOCX for simple documents (headings, paragraphs, inline formatting, links, page numbers). This is the SSI (Smallest Shippable Increment).
**Risk:** Medium. Open XML SDK boilerplate is tedious. The `ParagraphBuilder` and run property construction are the bulk of the work. Heading style IDs must match Word's built-in style names exactly.

---

## Sprint 3: Tables, Images, and Lists (DOCX Rich Elements -- Part 1)

**Wave:** 2 (Rich Elements)
**Dependencies:** Sprint 2 (DocxEmitter, DocxAstVisitor infrastructure)
**Projects modified:** `Md2.Emit.Docx`, `Md2.Emit.Docx.Tests`, `Md2.Core` (smart typography transform)
**Lead:** Tara (tests) -> Sato (implementation)

**PROTOTYPE GATE: Table auto-sizing.** The first 2 issues in this sprint are the prototype. If the table prototype cannot handle 4 test cases (uniform, varying lengths, header row, one very long cell) within the time-boxed budget, STOP and reassess per ADR-0004 debate outcome. The gate criteria are in the issue description.

### Acceptance Criteria Covered

| ID | Summary | Priority |
|----|---------|----------|
| AC-4.1.1 | Tables render with themed borders | P0 |
| AC-4.1.2 | Header row visually distinct | P0 |
| AC-4.1.3 | Alternating row shading | P0 |
| AC-4.1.4 | Column widths auto-sized by content | P0 |
| AC-4.1.5 | Tables split across pages, header repeats | P0 |
| AC-4.1.6 | Table border width/color from theme | P0 |
| AC-4.1.7 | Cell padding consistent | P0 |
| AC-3.4.1 | Images embedded from relative paths | P0 |
| AC-3.4.2 | Image aspect ratio preserved, scaled to fit | P0 |
| AC-3.4.3 | Alt text set as image description | P0 |
| AC-3.4.4 | Missing images produce warning + placeholder | P0 |
| AC-3.3.1 | Unordered lists with bullets per nesting level | P0 |
| AC-3.3.2 | Ordered lists with sequential numbering | P0 |
| AC-3.3.3 | Nested mixed lists with correct indentation | P0 |
| AC-3.3.4 | Task list items with checkbox characters | P0 |
| AC-3.3.5 | List items with block-level content | P0 |
| AC-2.1.1 | Smart quotes | P1 |
| AC-2.1.2 | En-dash, em-dash | P1 |
| AC-2.1.3 | Ellipsis | P1 |
| AC-2.1.4 | Smart typography skips code spans | P1 |

### Issues

| # | Title | Size | Criteria |
|---|-------|------|----------|
| 15 | **PROTOTYPE GATE** feat(emit-docx): TableBuilder with auto-sizing column width heuristic | L | AC-4.1.4 |
| 16 | feat(emit-docx): table styling (borders, header row, alternating rows, cell padding, page split) | L | AC-4.1.1, AC-4.1.2, AC-4.1.3, AC-4.1.5, AC-4.1.6, AC-4.1.7 |
| 17 | feat(emit-docx): ImageBuilder with aspect ratio, scaling, alt text, missing file handling | M | AC-3.4.1, AC-3.4.2, AC-3.4.3, AC-3.4.4 |
| 18 | feat(emit-docx): ListBuilder with numbered, bulleted, nested, and task lists | L | AC-3.3.1, AC-3.3.2, AC-3.3.3, AC-3.3.4, AC-3.3.5 |
| 19 | feat(core): SmartTypographyTransform (quotes, dashes, ellipsis, code-span exclusion) | M | AC-2.1.1, AC-2.1.2, AC-2.1.3, AC-2.1.4 |

**Sprint 3 total:** 5 issues, ~20 acceptance criteria
**Sprint 3 exit:** Tables, images, and lists render in DOCX. The table prototype gate has been cleared. Smart typography is active.
**Risk:** HIGH. Table auto-sizing is the single largest implementation risk (ADR-0004 debate). The 5-day prototype gate is the go/no-go decision. If the gate fails, Grace escalates and the plan adjusts (GemBox evaluation or simplified table layout as v1 fallback).

---

## Sprint 4: Code Blocks, Blockquotes, Footnotes, Admonitions, Definition Lists

**Wave:** 2 (Rich Elements)
**Dependencies:** Sprint 2 (DocxEmitter infrastructure). Independent of Sprint 3 (different builders).
**Projects created:** `Md2.Highlight`, `Md2.Highlight.Tests`
**Projects modified:** `Md2.Emit.Docx`, `Md2.Core`
**Lead:** Tara (tests) -> Sato (implementation)

### Acceptance Criteria Covered

| ID | Summary | Priority |
|----|---------|----------|
| AC-4.2.1 | Code blocks with mono font and background | P0 |
| AC-4.2.2 | Syntax highlighting with TextMateSharp colors | P0 |
| AC-4.2.3 | Code blocks without language -- no highlighting | P0 |
| AC-4.2.4 | 20+ languages supported | P0 |
| AC-4.2.5 | Long lines wrap or truncate | P0 |
| AC-4.2.6 | Code block border from theme | P1 |
| AC-4.3.1 | Blockquotes with left border and italic | P1 |
| AC-4.3.2 | Nested blockquotes increase indentation | P1 |
| AC-4.3.3 | Blockquotes containing block elements | P1 |
| AC-4.4.1 | Footnote references as superscript | P1 |
| AC-4.4.2 | Footnote definitions at end/bottom | P1 |
| AC-4.4.3 | Bidirectional footnote navigation | P1 |
| AC-4.5.1 | Admonitions with distinct colors per type | P1 |
| AC-4.5.2 | Admonition label in bold | P1 |
| AC-4.5.3 | Inline formatting within admonitions | P1 |
| AC-4.5.4 | Custom admonition titles | P1 |
| AC-4.6.1 | Definition terms bold | P1 |
| AC-4.6.2 | Definitions indented below term | P1 |
| AC-4.6.3 | Multiple definitions per term | P1 |

### Issues

| # | Title | Size | Criteria |
|---|-------|------|----------|
| 20 | feat(highlight): CodeTokenizer with TextMateSharp, HighlightThemeMapper | M | AC-4.2.4 (language coverage) |
| 21 | feat(core): SyntaxHighlightAnnotator transform | S | (attaches tokens to AST) |
| 22 | feat(emit-docx): CodeBlockBuilder with syntax-highlighted runs, background, border, line wrapping | L | AC-4.2.1, AC-4.2.2, AC-4.2.3, AC-4.2.5, AC-4.2.6 |
| 23 | feat(emit-docx): blockquote rendering with left border, nesting, block content | M | AC-4.3.1, AC-4.3.2, AC-4.3.3 |
| 24 | feat(emit-docx): FootnoteBuilder with superscript refs and bidirectional links | M | AC-4.4.1, AC-4.4.2, AC-4.4.3 |
| 25 | feat(emit-docx): AdmonitionBuilder with typed styling and custom titles | M | AC-4.5.1, AC-4.5.2, AC-4.5.3, AC-4.5.4 |
| 26 | feat(emit-docx): definition list rendering (bold terms, indented definitions) | S | AC-4.6.1, AC-4.6.2, AC-4.6.3 |

**Sprint 4 total:** 7 issues, ~19 acceptance criteria
**Sprint 4 exit:** All non-Playwright element types emit to DOCX. Syntax highlighting works for 20+ languages.
**Risk:** Medium. TextMateSharp's Oniguruma native binary needs deployment verification (ADR-0007 debate). Footnote implementation requires understanding Word's footnote part structure in Open XML.

---

## Sprint 5: Math (KaTeX/OMML) and Mermaid (Playwright)

**Wave:** 2 (Rich Elements)
**Dependencies:** Sprint 2 (DocxEmitter). Playwright infrastructure is new.
**Projects created:** `Md2.Math`, `Md2.Math.Tests`, `Md2.Diagrams`, `Md2.Diagrams.Tests`
**Projects modified:** `Md2.Core`
**Lead:** Tara (tests) -> Sato (implementation)

**Both Math and Mermaid depend on Playwright/Chromium.** This sprint establishes the shared Playwright infrastructure (`BrowserManager`) first, then builds both features on it. This is the correct ordering because: (a) KaTeX runs in Playwright for LaTeX -> MathML conversion (ADR-0006 revised), (b) Mermaid runs in Playwright for diagram rendering, (c) sharing the browser instance across both avoids double cold-start.

### Acceptance Criteria Covered

| ID | Summary | Priority |
|----|---------|----------|
| AC-4.7.1 | Inline math as inline OMML | P1 |
| AC-4.7.2 | Display math as centered OMML | P1 |
| AC-4.7.3 | LaTeX constructs: frac, superscript, subscript, Greek, sum, int, sqrt, matrix | P1 |
| AC-4.7.4 | Graceful degradation without Chromium (code fallback) | P1 |
| AC-4.7.5 | Math performance: 25 expressions < 10 seconds | P1 |
| AC-4.8.1 | Mermaid -> PNG at 2x DPI | P1 |
| AC-4.8.2 | Diagrams embedded at correct position | P1 |
| AC-4.8.3 | --no-mermaid renders as code blocks | P1 |
| AC-4.8.4 | Graceful degradation without Chromium | P1 |
| AC-4.8.5 | Diagram caching by content hash | P1 |
| AC-4.8.6 | --mermaid-js flag for user-supplied JS | P1 |
| AC-4.8.7 | Performance: 10 diagrams < 15 seconds | P1 |

### Issues

| # | Title | Size | Criteria |
|---|-------|------|----------|
| 27 | feat(diagrams): BrowserManager for shared Playwright/Chromium lifecycle | M | (infrastructure for Math + Mermaid) |
| 28 | feat(math): KaTeX-via-Playwright for LaTeX -> MathML conversion | L | AC-4.7.1, AC-4.7.2, AC-4.7.3 |
| 29 | feat(math): MML2OMML.xsl integration via XslCompiledTransform | M | AC-4.7.1, AC-4.7.2, AC-4.7.3 |
| 30 | feat(math): MathBlockAnnotator transform with graceful degradation | M | AC-4.7.4, AC-4.7.5 |
| 31 | feat(emit-docx): MathBuilder for inline and display OMML elements | S | AC-4.7.1, AC-4.7.2 |
| 32 | feat(diagrams): MermaidRenderer with PNG output, DPI scaling, caching | L | AC-4.8.1, AC-4.8.5 |
| 33 | feat(diagrams): MermaidDiagramRenderer transform with --no-mermaid, --mermaid-js, graceful degradation | M | AC-4.8.2, AC-4.8.3, AC-4.8.4, AC-4.8.6 |
| 34 | perf(diagrams+math): benchmark suite for 10 diagrams + 25 expressions | S | AC-4.7.5, AC-4.8.7 |

**Sprint 5 total:** 8 issues, ~12 acceptance criteria
**Sprint 5 exit:** Math and Mermaid both render in DOCX via Playwright. Performance benchmarks pass. Graceful degradation works without Chromium.
**Risk:** HIGH. This sprint has the second-highest technical risk.
- **MML2OMML.xsl licensing:** Must verify redistribution rights before shipping. See ADR-0006 debate notes.
- **KaTeX-via-Playwright integration:** Novel approach. No known C# precedent for running KaTeX server-side in Playwright. Prototype early in the sprint.
- **Playwright cold-start:** First run downloads Chromium (~300MB). Must handle this gracefully.
- **Performance budgets:** 10 diagrams < 15s and 25 math expressions < 10s are aggressive targets. May need browser instance pooling.

---

## Sprint 6: Theme Engine and Style Cascade

**Wave:** 3 (Style System)
**Dependencies:** Sprint 2 (hardcoded default theme replaced by real theme engine)
**Projects created:** `Md2.Themes`, `Md2.Themes.Tests`
**Projects modified:** `Md2.Emit.Docx` (swap hardcoded theme for real ResolvedTheme), `Md2.Cli` (--theme, --preset, --template, --style flags)
**Lead:** Tara (tests) -> Sato (implementation)

### Acceptance Criteria Covered

| ID | Summary | Priority |
|----|---------|----------|
| AC-5.2.1 | YAML theme loaded via --theme, overrides preset | P1 |
| AC-5.2.2 | Theme YAML supports all documented properties | P1 |
| AC-5.2.3 | Partial theme YAML falls through to next cascade layer | P1 |
| AC-5.2.4 | Invalid theme YAML produces clear error with path and expected type | P1 |
| AC-5.2.5 | Unknown properties ignored for forward compat | P1 |
| AC-5.2.6 | ${...} syntax produces clear "not supported" warning | P1 |
| AC-5.3.1 | --template loads DOCX as lowest cascade layer | P0 |
| AC-5.3.2 | Missing template styles filled from preset with warnings | P0 |
| AC-5.3.3 | Template styles do not override higher cascade layers | P0 |
| AC-5.3.4 | IRM-protected template detected with error + guidance | P0 |
| AC-5.3.5 | Legacy .doc produces specific error | P0 |
| AC-5.3.6 | .docm refused by default with warning | P1 |
| AC-5.3.7 | Template size limit with configurable --max-template-size | P1 |
| AC-5.4.1 | md2 theme resolve displays cascade table | P1 |
| AC-5.4.2 | theme resolve accepts all 4 flags | P1 |
| AC-5.4.3 | theme resolve with no flags shows default | P1 |
| AC-3.5.4 | Header text configurable via theme | P1 |
| AC-9.1.4 | subject/keywords from front matter | P1 |

### Issues

| # | Title | Size | Criteria |
|---|-------|------|----------|
| 35 | feat(themes): ThemeParser for YAML -> ThemeDefinition with validation | L | AC-5.2.1, AC-5.2.2, AC-5.2.4, AC-5.2.5, AC-5.2.6 |
| 36 | feat(themes): ThemeCascadeResolver with 4-layer merge | L | AC-5.2.3, AC-5.3.1, AC-5.3.3 |
| 37 | feat(themes): DocxStyleExtractor for template -> ThemeDefinition (Layer 1) | M | AC-5.3.1, AC-5.3.2 |
| 38 | feat(themes): template safety (IRM detection, .doc detection, .docm refusal, size limit) | M | AC-5.3.4, AC-5.3.5, AC-5.3.6, AC-5.3.7 |
| 39 | feat(cli): --theme, --preset, --template, --style flags wired to cascade | M | AC-5.2.1, AC-5.3.1, AC-5.3.3 |
| 40 | feat(cli): md2 theme resolve command | M | AC-5.4.1, AC-5.4.2, AC-5.4.3 |
| 41 | feat(emit-docx): replace hardcoded theme with ResolvedTheme from cascade | M | (infrastructure) |
| 42 | feat(emit-docx): header/footer from theme, subject/keywords doc properties | S | AC-3.5.4, AC-9.1.4 |

**Sprint 6 total:** 8 issues, ~18 acceptance criteria
**Sprint 6 exit:** Full 4-layer cascade works. All --theme/--preset/--template/--style flags functional. `md2 theme resolve` shows cascade debugging. Template safety guards in place.
**Risk:** Medium. The cascade resolver is the most complex piece -- merging 4 layers with property-level precedence requires careful testing. Theme extraction from DOCX templates is best-effort but must handle diverse real-world templates.

---

## Sprint 7: Style Presets and Theme Management Commands

**Wave:** 3 (Style System)
**Dependencies:** Sprint 6 (theme engine, cascade resolver)
**Projects modified:** `Md2.Themes` (presets), `Md2.Cli` (theme commands)
**Lead:** Tara (tests) -> Sato (implementation)

### Acceptance Criteria Covered

| ID | Summary | Priority |
|----|---------|----------|
| AC-5.1.1 | 5 presets available (default, technical, corporate, academic, minimal) | P2 |
| AC-5.1.2 | Each preset visually distinct | P2 |
| AC-5.1.4 | Each preset is a standalone YAML file | P2 |
| AC-6.1.1 | md2 theme extract produces valid YAML from DOCX | P1 |
| AC-6.1.2 | Extracted YAML includes comments | P1 |
| AC-6.1.3 | Extracted theme round-trips | P1 |
| AC-6.2.1 | md2 theme validate reports errors with line numbers | P1 |
| AC-6.2.2 | Valid theme reports success | P1 |
| AC-6.2.3 | Unusually small/large values produce warnings | P2 |
| AC-6.3.1 | md2 theme list shows preset names and descriptions | P1 |
| AC-2.2.1 | TOC generated from headings when --toc specified | P1 |
| AC-2.2.2 | TOC entries hyperlinked to headings | P1 |
| AC-2.2.3 | TOC depth defaults to 3, configurable | P1 |
| AC-2.2.4 | TOC styled per theme | P1 |
| AC-2.3.1 | Cover page generated from front matter when --cover | P1 |
| AC-2.3.2 | Cover page displays title, subtitle, author, date, abstract | P1 |
| AC-2.3.3 | Cover page followed by section break | P1 |
| AC-2.3.4 | --cover without title -> warning, skip cover | P1 |
| AC-2.4.1 | Headings assigned bookmark IDs | P1 |
| AC-2.4.2 | Internal links -> hyperlinks to bookmarks | P1 |
| AC-2.4.3 | Duplicate headings get unique bookmark IDs | P1 |

### Issues

| # | Title | Size | Criteria |
|---|-------|------|----------|
| 43 | feat(themes): author 5 preset YAML files (default, technical, corporate, academic, minimal) | L | AC-5.1.1, AC-5.1.2, AC-5.1.4 |
| 44 | feat(themes): PresetRegistry for loading embedded preset files | S | AC-5.1.1 |
| 45 | feat(cli): md2 theme extract command | M | AC-6.1.1, AC-6.1.2, AC-6.1.3 |
| 46 | feat(cli): md2 theme validate command | M | AC-6.2.1, AC-6.2.2, AC-6.2.3 |
| 47 | feat(cli): md2 theme list command | S | AC-6.3.1 |
| 48 | feat(core): TocGenerator transform + DOCX TOC emission | L | AC-2.2.1, AC-2.2.2, AC-2.2.3, AC-2.2.4 |
| 49 | feat(core): CoverPageGenerator transform + DOCX cover page emission | M | AC-2.3.1, AC-2.3.2, AC-2.3.3, AC-2.3.4 |
| 50 | feat(core): CrossReferenceLinker transform + bookmark emission | M | AC-2.4.1, AC-2.4.2, AC-2.4.3 |

**Sprint 7 total:** 8 issues, ~21 acceptance criteria
**Sprint 7 exit:** All 5 presets authored and visually reviewed. Theme extract/validate/list commands work. TOC, cover page, and cross-references all functional.
**Risk:** Medium. Preset authoring requires visual design judgment -- the human must review and approve all 5 presets (Done Gate item 7). TOC generation in Open XML requires understanding Word's built-in TOC field codes. Cover page section breaks need careful OOXML construction.

---

## Sprint 8: Preview, Multi-File, Diagnostics, Pipeline Inspection, E2E Polish

**Wave:** 4 (Polish)
**Dependencies:** Sprints 1-7 (all features built, this sprint polishes and integrates)
**Projects created:** `Md2.Preview`, `Md2.Preview.Tests`, `Md2.Emit.Pptx` (stub)
**Projects modified:** `Md2.Cli`, `Md2.Integration.Tests`
**Lead:** Tara (tests) -> Sato (implementation) -> Vik + Pierrot (final review)

### Acceptance Criteria Covered

| ID | Summary | Priority |
|----|---------|----------|
| AC-8.1.1 | md2 preview opens browser with rendered doc | P1 |
| AC-8.1.2 | Hot-reload on file save < 500ms | P1 |
| AC-8.1.3 | Preview uses same theme as DOCX | P1 |
| AC-8.1.4 | Ctrl+C cleanly stops preview | P1 |
| AC-8.1.5 | Preview works without --template | P1 |
| AC-7.2.1 | --dry-run prints summary, no output file | P1 |
| AC-7.2.2 | --stage parse --emit json dumps AST | P1 |
| AC-7.2.3 | --stage transform --emit json dumps transformed AST | P1 |
| AC-7.3.1 | md2 doctor checks runtime, Playwright, Chromium, TextMateSharp | P1 |
| AC-7.3.2 | Each check reports pass/fail/warning with guidance | P1 |
| AC-7.3.3 | md2 doctor exit code 0/1 | P1 |
| AC-7.1.6 | --verbose shows timing and cascade details | P1 |
| AC-7.4.1 | Multi-file concatenation | P2 |
| AC-7.4.2 | Front matter merge (first file wins) | P2 |
| AC-7.4.3 | Relative paths resolve per file | P2 |
| AC-7.4.4 | Page breaks between files (configurable) | P2 |
| AC-3.4.5 | Image captions | P2 |

**Also in this sprint:**
- `Md2.Emit.Pptx` stub (implements `IFormatEmitter` with `NotSupportedException("PPTX support is planned for v2")`)
- `AdmonitionTransform` (normalize admonition syntax, order 090)
- Full E2E integration test suite with 20-page representative document
- Visual review of all 5 presets by human (Done Gate item 7)
- `md2 doctor` diagnostic command
- Cross-platform verification (Windows + Linux)

### Issues

| # | Title | Size | Criteria |
|---|-------|------|----------|
| 51 | feat(preview): PreviewServer with embedded HTTP and HtmlPreviewRenderer | L | AC-8.1.1, AC-8.1.3, AC-8.1.5 |
| 52 | feat(preview): FileWatcher with hot-reload signaling | M | AC-8.1.2, AC-8.1.4 |
| 53 | feat(cli): --dry-run, --stage, --emit pipeline inspection flags | M | AC-7.2.1, AC-7.2.2, AC-7.2.3 |
| 54 | feat(cli): md2 doctor diagnostic command | M | AC-7.3.1, AC-7.3.2, AC-7.3.3 |
| 55 | feat(cli): --verbose timing and cascade details | S | AC-7.1.6 |
| 56 | feat(cli): multi-file concatenation with --no-file-break | M | AC-7.4.1, AC-7.4.2, AC-7.4.3, AC-7.4.4 |
| 57 | feat(emit-docx): image caption rendering | S | AC-3.4.5 |
| 58 | feat(emit-pptx): stub IFormatEmitter with v2 message | S | (seam for v2) |
| 59 | test(integration): full E2E test with 20-page representative document | L | (Done Gate item 9) |
| 60 | chore: cross-platform verification (Windows + Linux) | M | (Done Gate item 8) |

**Sprint 8 total:** 10 issues, ~17 acceptance criteria + Done Gate validation
**Sprint 8 exit:** v1 is complete. All P0 criteria pass. All P1 criteria pass (or have documented deferrals). All 5 presets visually approved. `md2 doctor` green on Windows + Linux. E2E test passes.
**Risk:** Medium. Preview requires an HTML rendering path that approximates the DOCX output. This is inherently imperfect. The multi-file feature is P2 and can be deferred if time is short. The main risk is the E2E test revealing integration issues between independently-built components.

---

## Full Issue Index

### Sprint 1 -- Parsing Foundation and Core Pipeline (7 issues)

| Issue | Title | Size | Sprint |
|-------|-------|------|--------|
| 1 | feat(parsing): configure Markdig pipeline with CommonMark + GFM extensions | M | 1 |
| 2 | feat(parsing): custom admonition block parser extension | M | 1 |
| 3 | feat(parsing): register definition list, attribute, math, footnote extensions | S | 1 |
| 4 | feat(parsing): YAML front matter extraction with DocumentMetadata mapping | M | 1 |
| 5 | feat(core): pipeline skeleton with IAstTransform, IFormatEmitter, ConversionPipeline | M | 1 |
| 6 | feat(core): typed AST extension methods and AstDataKeys | S | 1 |
| 7 | feat(core): YamlFrontMatterExtractor transform | S | 1 |

### Sprint 2 -- Basic DOCX Emission and CLI Skeleton (7 issues)

| Issue | Title | Size | Sprint |
|-------|-------|------|--------|
| 8 | feat(emit-docx): DocxEmitter skeleton with ParagraphBuilder for headings and body text | L | 2 |
| 9 | feat(emit-docx): inline run formatting (bold, italic, strikethrough, inline code, hyperlinks, line breaks) | M | 2 |
| 10 | feat(emit-docx): page layout (page size, margins, page numbers in footer) | M | 2 |
| 11 | feat(emit-docx): document properties from front matter metadata | S | 2 |
| 12 | feat(cli): root command, convert command, global options | M | 2 |
| 13 | feat(core): hardcoded default ResolvedTheme (placeholder until theme engine) | S | 2 |
| 14 | test(integration): first E2E test -- Markdown in, DOCX out, validate structure | M | 2 |

### Sprint 3 -- Tables, Images, Lists (5 issues)

| Issue | Title | Size | Sprint |
|-------|-------|------|--------|
| 15 | **GATE** feat(emit-docx): TableBuilder with auto-sizing column width heuristic | L | 3 |
| 16 | feat(emit-docx): table styling (borders, header row, alternating rows, cell padding, page split) | L | 3 |
| 17 | feat(emit-docx): ImageBuilder with aspect ratio, scaling, alt text, missing file handling | M | 3 |
| 18 | feat(emit-docx): ListBuilder with numbered, bulleted, nested, and task lists | L | 3 |
| 19 | feat(core): SmartTypographyTransform | M | 3 |

### Sprint 4 -- Code, Blockquotes, Footnotes, Admonitions, Definitions (7 issues)

| Issue | Title | Size | Sprint |
|-------|-------|------|--------|
| 20 | feat(highlight): CodeTokenizer with TextMateSharp, HighlightThemeMapper | M | 4 |
| 21 | feat(core): SyntaxHighlightAnnotator transform | S | 4 |
| 22 | feat(emit-docx): CodeBlockBuilder with syntax-highlighted runs | L | 4 |
| 23 | feat(emit-docx): blockquote rendering with left border, nesting, block content | M | 4 |
| 24 | feat(emit-docx): FootnoteBuilder with superscript refs and bidirectional links | M | 4 |
| 25 | feat(emit-docx): AdmonitionBuilder with typed styling and custom titles | M | 4 |
| 26 | feat(emit-docx): definition list rendering | S | 4 |

### Sprint 5 -- Math and Mermaid (8 issues)

| Issue | Title | Size | Sprint |
|-------|-------|------|--------|
| 27 | feat(diagrams): BrowserManager for shared Playwright/Chromium lifecycle | M | 5 |
| 28 | feat(math): KaTeX-via-Playwright for LaTeX -> MathML | L | 5 |
| 29 | feat(math): MML2OMML.xsl integration via XslCompiledTransform | M | 5 |
| 30 | feat(math): MathBlockAnnotator transform with graceful degradation | M | 5 |
| 31 | feat(emit-docx): MathBuilder for inline and display OMML elements | S | 5 |
| 32 | feat(diagrams): MermaidRenderer with PNG output, DPI scaling, caching | L | 5 |
| 33 | feat(diagrams): MermaidDiagramRenderer transform | M | 5 |
| 34 | perf(diagrams+math): benchmark suite | S | 5 |

### Sprint 6 -- Theme Engine and Cascade (8 issues)

| Issue | Title | Size | Sprint |
|-------|-------|------|--------|
| 35 | feat(themes): ThemeParser for YAML -> ThemeDefinition with validation | L | 6 |
| 36 | feat(themes): ThemeCascadeResolver with 4-layer merge | L | 6 |
| 37 | feat(themes): DocxStyleExtractor for template -> ThemeDefinition | M | 6 |
| 38 | feat(themes): template safety (IRM, .doc, .docm, size limit) | M | 6 |
| 39 | feat(cli): --theme, --preset, --template, --style flags | M | 6 |
| 40 | feat(cli): md2 theme resolve command | M | 6 |
| 41 | feat(emit-docx): replace hardcoded theme with ResolvedTheme | M | 6 |
| 42 | feat(emit-docx): header/footer from theme, subject/keywords properties | S | 6 |

### Sprint 7 -- Presets and Theme Commands (8 issues)

| Issue | Title | Size | Sprint |
|-------|-------|------|--------|
| 43 | feat(themes): author 5 preset YAML files | L | 7 |
| 44 | feat(themes): PresetRegistry for loading embedded presets | S | 7 |
| 45 | feat(cli): md2 theme extract command | M | 7 |
| 46 | feat(cli): md2 theme validate command | M | 7 |
| 47 | feat(cli): md2 theme list command | S | 7 |
| 48 | feat(core): TocGenerator transform + DOCX TOC emission | L | 7 |
| 49 | feat(core): CoverPageGenerator transform + DOCX cover page emission | M | 7 |
| 50 | feat(core): CrossReferenceLinker transform + bookmark emission | M | 7 |

### Sprint 8 -- Polish and Integration (10 issues)

| Issue | Title | Size | Sprint |
|-------|-------|------|--------|
| 51 | feat(preview): PreviewServer with embedded HTTP and HtmlPreviewRenderer | L | 8 |
| 52 | feat(preview): FileWatcher with hot-reload signaling | M | 8 |
| 53 | feat(cli): --dry-run, --stage, --emit pipeline inspection flags | M | 8 |
| 54 | feat(cli): md2 doctor diagnostic command | M | 8 |
| 55 | feat(cli): --verbose timing and cascade details | S | 8 |
| 56 | feat(cli): multi-file concatenation with --no-file-break | M | 8 |
| 57 | feat(emit-docx): image caption rendering | S | 8 |
| 58 | feat(emit-pptx): stub IFormatEmitter with v2 message | S | 8 |
| 59 | test(integration): full E2E test with 20-page representative document | L | 8 |
| 60 | chore: cross-platform verification (Windows + Linux) | M | 8 |

**Total:** 60 issues across 8 sprints

### Size Distribution

| Size | Count | Estimated Effort |
|------|-------|-----------------|
| S (< 1 hour) | 14 | ~14 hours |
| M (1-3 hours) | 30 | ~60 hours |
| L (3-8 hours) | 16 | ~80 hours |
| **Total** | **60** | **~154 hours** |

---

## Risk Register (Plan-Level)

| ID | Risk | Sprint | Severity | Mitigation | Gate? |
|----|------|--------|----------|------------|-------|
| R1 | Table auto-sizing fails prototype gate | 3 | Critical | 5-day time-box. Fallback: GemBox evaluation or simplified percentage-based widths | YES |
| R2 | KaTeX-via-Playwright integration novel | 5 | High | Prototype BrowserManager first. If KaTeX approach fails, fall back to direct LaTeX-to-OMML for simple expressions + PNG for complex | NO |
| R3 | MML2OMML.xsl licensing unclear | 5 | Medium | Verify redistribution rights before Sprint 5. Fallback: implement OMML construction directly | NO |
| R4 | TextMateSharp Oniguruma native binary | 4 | Medium | Test single-file publish on Linux. Fallback: Shiki-via-Playwright | NO |
| R5 | Playwright cold-start UX | 5, 8 | Medium | Progress bar for Chromium download. Cache browser instance. md2 doctor checks | NO |
| R6 | Theme extraction from corporate templates | 6 | Medium | Best-effort with warnings. Test with 3-5 real templates | NO |
| R7 | Word footnote part structure complexity | 4 | Low | Open XML SDK documentation. Reference existing implementations | NO |
| R8 | Preview HTML approximation of DOCX | 8 | Low | Accept imperfection. "Close approximation" is the requirement, not pixel-perfect | NO |

---

## Sprint 1 -- Ready Items

The following issues are ready for immediate work. They should be moved to "Ready" on the board at Sprint 1 kickoff.

1. **Issue 1** -- feat(parsing): configure Markdig pipeline with CommonMark + GFM extensions
2. **Issue 2** -- feat(parsing): custom admonition block parser extension
3. **Issue 3** -- feat(parsing): register definition list, attribute, math, footnote extensions
4. **Issue 4** -- feat(parsing): YAML front matter extraction with DocumentMetadata mapping
5. **Issue 5** -- feat(core): pipeline skeleton with IAstTransform, IFormatEmitter, ConversionPipeline
6. **Issue 6** -- feat(core): typed AST extension methods and AstDataKeys
7. **Issue 7** -- feat(core): YamlFrontMatterExtractor transform

**Recommended execution order within Sprint 1:**
1. Issue 5 (pipeline skeleton -- everyone depends on this)
2. Issue 6 (typed AST extensions -- transforms need this)
3. Issue 1 (Markdig pipeline -- parsing foundation)
4. Issues 2, 3 (Markdig extensions -- can be parallel)
5. Issue 4 (front matter extraction -- needs Markdig pipeline)
6. Issue 7 (front matter transform -- needs pipeline + extraction)

---

## Session Handoff Format

For `/handoff` consumption at the end of each sprint:

```
## Sprint N Complete

**Status:** Done / Partially Done / Blocked
**Issues completed:** #X, #Y, #Z
**Issues deferred:** #A (reason)
**Issues added mid-sprint:** #B (reason)

### Key Decisions Made
- Chose X over Y because Z

### Blockers / Open Questions
- ...

### Next Sprint (N+1)
- Issues: #...
- Key risk: ...
- Dependencies resolved: ...

### Tech Debt Incurred
- TD-XXX: description (logged in docs/tech-debt.md)

### Files Changed
- path/to/file (what changed)
```
