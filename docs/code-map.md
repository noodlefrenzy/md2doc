---
agent-notes:
  ctx: "codebase structural overview for humans and agents"
  deps: [docs/architecture.md]
  state: active
  last: "grace@2026-03-14"
  key: ["UPDATE when adding packages, modules, or changing public APIs"]
---
# Code Map

Structural overview of the md2 codebase. Use this to orient yourself before diving into code. **Keep this up to date** -- when you add a package, module, or significantly change a public API, update this file.

Read this file at the start of every session instead of exploring the codebase from scratch.

## Architecture at a Glance

```
Markdown (.md)
  |
  v
[Md2.Parsing]  Markdig pipeline: CommonMark + GFM + extensions
  |
  v
MarkdownDocument  (Markdig native AST)
  |
  v
[Md2.Core]  Ordered AST transforms:
  |         front-matter, smart-typo, math, mermaid,
  |         syntax-highlight, TOC, cover, cross-refs, admonitions
  |
  |         +-- [Md2.Highlight]  TextMateSharp tokenization
  |         +-- [Md2.Math]       LaTeX -> OMML converter
  |         +-- [Md2.Diagrams]   Mermaid -> PNG via Playwright
  |
  v
MarkdownDocument  (transformed, annotated AST)
  +
ResolvedTheme  (4-layer cascade: CLI > YAML > preset > template)
  |
  v
[Md2.Emit.Docx]   Open XML SDK -> .docx
[Md2.Emit.Pptx]   Open XML SDK -> .pptx  (v2, stubbed)
  |
  v
Output file (.docx / .pptx)
```

## Dependency Graph

```
Md2.Cli ─────────────── entry point, System.CommandLine
  +-- Md2.Core ──────── pipeline orchestration, transforms, shared types
  |     +-- Md2.Parsing ── Markdig config, extension registration, front matter
  |     |     +-- Markdig (NuGet)
  |     |     +-- YamlDotNet (NuGet)
  |     +-- Md2.Themes ─── YAML theme DSL, cascade resolver, presets
  |     |     +-- YamlDotNet (NuGet)
  |     +-- Md2.Highlight ─ syntax highlighting
  |     |     +-- TextMateSharp (NuGet)
  |     +-- Md2.Math ────── LaTeX-to-OMML (no external deps)
  |     +-- Md2.Diagrams ── (via interface, not direct ref)
  +-- Md2.Emit.Docx ──── DOCX emitter
  |     +-- Md2.Core
  |     +-- DocumentFormat.OpenXml (NuGet)
  +-- Md2.Slides ──────── MARP parser (v2)
  |     +-- Md2.Core
  |     +-- Md2.Parsing
  |     +-- Markdig (NuGet)
  |     +-- YamlDotNet (NuGet)
  +-- Md2.Emit.Pptx ──── PPTX emitter (v2)
  |     +-- Md2.Core
  |     +-- Md2.Slides
  |     +-- DocumentFormat.OpenXml (NuGet)
  +-- Md2.Diagrams ────── Mermaid rendering
  |     +-- Microsoft.Playwright (NuGet)
  +-- Md2.Preview ─────── HTML preview with hot-reload
        +-- Microsoft.Playwright (NuGet)
        +-- Md2.Core
```

**Key dependency rule:** Emitter projects depend on Md2.Core. Md2.Core does NOT depend on any emitter. The CLI wires emitters via the `IFormatEmitter` interface.

## Package / Module Summaries

### Md2.Cli -- Console Entry Point

**Purpose:** CLI argument parsing, command routing, output formatting.

| Area | Key Types | Notes |
|------|----------|-------|
| Root command | `ConvertCommand` | Default: infer format from -o extension |
| Theme commands | `ThemeResolveCommand` | `md2 theme resolve` with cascade trace |
| Preview command | `PreviewCommand` | Hot-reload preview via Playwright |
| Doctor command | `DoctorCommand` | Environment diagnostics (runtime, Chromium, TextMateSharp) |
| Pipeline wiring | `PipelineFactory` | Composes pipeline from CLI options |

**External deps:** System.CommandLine, Spectre.Console (output formatting)

### Md2.Core -- Pipeline Orchestration

**Purpose:** Defines the conversion pipeline, AST transform interface, emitter interface, shared types.

| Area | Key Types | Notes |
|------|----------|-------|
| Pipeline | `ConversionPipeline`, `TransformResult` | Parse -> Transform -> Style Resolve -> Emit |
| Transforms | `IAstTransform`, `TransformContext` | Ordered visitors over Markdig AST |
| Emitter | `IFormatEmitter`, `EmitOptions` | Format-agnostic emit contract |
| Types | `ResolvedTheme`, `ResolvedPptxTheme`, `StyleWarning`, `DocumentMetadata` | Shared value types, PPTX sub-object per ADR-0016 |
| AST keys | `AstDataKeys` | Static keys for SetData/GetData |

### Md2.Parsing -- Markdown Parser Configuration

**Purpose:** Configures and runs the Markdig pipeline with all required extensions.

| Area | Key Types | Notes |
|------|----------|-------|
| Pipeline builder | `Md2MarkdownPipeline` | Registers all extensions |
| Front matter | `FrontMatterExtractor` | YAML front matter -> DocumentMetadata |
| Custom extensions | `AdmonitionExtension`, `AdmonitionBlock` | Custom parser for admonition syntax |

**External deps:** Markdig, YamlDotNet

### Md2.Themes -- Theme Engine

**Purpose:** YAML theme parsing, preset management, 4-layer cascade resolution.

| Area | Key Types | Notes |
|------|----------|-------|
| Parsing | `ThemeDefinition`, `ThemeParser` | YAML -> typed model |
| Cascade | `ThemeCascadeResolver`, `ThemeCascadeInput` | 4-layer merge |
| Presets | `PresetRegistry` | Loads embedded preset YAML files |
| Validation | `ThemeValidator` | Schema validation with line numbers |
| Formatting | `ThemeResolveFormatter` | Cascade trace -> aligned table output |
| Safety | `TemplateSafetyChecker` | IRM, .doc, .docm, size limit checks |

**External deps:** YamlDotNet

### Md2.Slides -- MARP Parser (v2)

**Purpose:** Parses MARP-styled Markdown into `SlideDocument` IR. Handles directives, slide splitting, image syntax, layout inference.

| Area | Key Types | Notes |
|------|----------|-------|
| Parser | `MarpParser` | Top-level: string → SlideDocument |
| Directives | `MarpDirectiveExtractor`, `MarpDirectiveClassifier`, `MarpDirectiveCascader` | Extract, classify (global/local/scoped), cascade |
| Slide splitting | `MarpSlideExtractor` | Split AST at `---` boundaries, extract speaker notes |
| Image syntax | `MarpImageSyntax`, `MarpImageInfo` | Parse bg, w:, h:, fit, split keywords |
| Extensions | `MarpExtensionParser`, `Md2Extension` | Parse `<!-- md2: {...} -->` YAML payloads |
| Layout | `SlideLayoutInferrer` | Infer layout from content + class directive |
| Theme mapping | `MarpThemeMapper` | Map MARP theme: directive → md2 preset hint |
| Types | `MarpDirective`, `MarpDirectiveScope` | Directive value types |

**External deps:** Markdig, YamlDotNet

### Md2.Emit.Pptx -- PPTX Emitter (v2)

**Purpose:** Produces Open XML PresentationDocument from SlideDocument with theme-based styling.

| Area | Key Types | Notes |
|------|----------|-------|
| Emitter | `PptxEmitter : ISlideEmitter` | Theme-based layouts, backgrounds, fit headings, tables, blockquotes, code blocks, hyperlinks, build animations, slide numbers |

**External deps:** DocumentFormat.OpenXml

### Md2.Emit.Docx -- DOCX Emitter

**Purpose:** Walks transformed AST, produces Open XML WordprocessingDocument.

| Area | Key Types | Notes |
|------|----------|-------|
| Emitter | `DocxEmitter : IFormatEmitter` | Top-level emitter |
| AST visitor | `DocxAstVisitor` | Dispatches to builders by node type |
| Builders | `ParagraphBuilder`, `TableBuilder`, `ListBuilder`, `CodeBlockBuilder`, `ImageBuilder`, `MathBuilder`, `AdmonitionBuilder`, `FootnoteBuilder` | One builder per element type |
| Styles | `DocxStyleApplicator` | Applies ResolvedTheme to OpenXml styles |
| Template | `TemplateManager` | Opens/creates template, merges styles |

**External deps:** DocumentFormat.OpenXml

### Md2.Highlight -- Syntax Highlighting

**Purpose:** Tokenizes code blocks using TextMate grammars, produces styled token lists.

| Area | Key Types | Notes |
|------|----------|-------|
| Tokenizer | `CodeTokenizer` | Code string + language -> token list |
| Theme mapping | `HighlightThemeMapper` | TextMate theme -> DOCX run colors |
| Transform | `SyntaxHighlightAnnotator : IAstTransform` | Attaches tokens to AST |

**External deps:** TextMateSharp

### Md2.Math -- LaTeX Math Converter

**Purpose:** Converts LaTeX math expressions to OMML via KaTeX (MathML) + MML2OMML.xsl (XSLT).

| Area | Key Types | Notes |
|------|----------|-------|
| Converter | `LatexToOmmlConverter` | LaTeX -> KaTeX (Playwright) -> MathML -> XSLT -> OMML |
| Transform | `MathBlockAnnotator : IAstTransform` | Attaches OMML to MathBlock/MathInline AST nodes |
| Resources | `katex.min.js`, `MML2OMML.xsl` | Embedded assembly resources |

**Deps:** Md2.Diagrams (BrowserManager), Md2.Core, Markdig (MathBlock/MathInline types)

### Md2.Diagrams -- Mermaid Rendering

**Purpose:** Renders Mermaid code blocks to high-resolution PNG via Playwright.

| Area | Key Types | Notes |
|------|----------|-------|
| Renderer | `MermaidRenderer` | Mermaid source -> PNG bytes |
| Browser | `BrowserManager` | Playwright lifecycle, Chromium setup |
| Cache | `DiagramCache` | SHA256 content-hash -> PNG path |
| Transform | `MermaidDiagramRenderer : IAstTransform` | Replaces code blocks with image refs |

**External deps:** Microsoft.Playwright

### Md2.Preview -- Preview with Hot-Reload

**Purpose:** Renders Markdown to HTML, serves via local HTTP, opens in Playwright-controlled browser with file watcher for hot-reload.

| Area | Key Types | Notes |
|------|----------|-------|
| Server | `PreviewServer` | Embedded HTTP server on localhost |
| Renderer | `HtmlPreviewRenderer` | AST -> themed HTML |
| Watcher | `FileWatcher` | Watches .md file, triggers re-render |

**External deps:** Microsoft.Playwright

## Key Type Flow

```
string (markdown)
  -> MarkdownDocument (Markdig AST)
    -> MarkdownDocument (transformed, with SetData annotations)
      + ResolvedTheme (merged from 4 layers)
        -> Stream (output .docx/.pptx bytes)
```

## Config Structure

| Source | Format | Purpose |
|--------|--------|---------|
| CLI args | System.CommandLine | Runtime options |
| theme.yaml | YAML | User style definitions |
| presets/*.yaml | YAML (embedded) | Built-in style presets |
| YAML front matter | YAML (in .md file) | Per-document metadata (title, author, date) |
| template.docx | OOXML | Custom DOCX template styles |

## Test Inventory

_To be populated as tests are written. See `docs/architecture.md` section 11 for the testing strategy._

| Package | Tests | Focus |
|---------|-------|-------|
| Md2.Core.Tests | 134 | Pipeline orchestration, transform ordering, SlideDocument IR, SlidePipeline, warnings |
| Md2.Parsing.Tests | 46 | Extension coverage, front matter extraction, YAML safety |
| Md2.Emit.Docx.Tests | 187 | Style application, element construction, contrast, path safety |
| Md2.Emit.Pptx.Tests | 22 | PPTX emission, slide count, speaker notes, metadata, theme backgrounds, fit headings, layout colors, tables, blockquotes, code blocks, hyperlinks, build animations, slide numbers |
| Md2.Slides.Tests | 135 | Directive extraction/classification/cascade, slide splitting, image syntax, extensions, layout inference, MarpParser, theme mapping |
| Md2.Themes.Tests | 213 | Theme parsing, cascade resolution, validation, formatting, PPTX section/cascade/validation |
| Md2.Highlight.Tests | 37 | Token accuracy, theme mapping |
| Md2.Math.Tests | 20 | LaTeX→OMML conversion, MathBlockAnnotator transform |
| Md2.Diagrams.Tests | 67 | BrowserManager, MermaidRenderer, DiagramCache, MermaidDiagramRenderer, theme config |
| Md2.Preview.Tests | 33 | HTML renderer, server endpoints, file watcher, CSS sanitization, security headers |
| Md2.Integration.Tests | 86 | End-to-end pipeline, composition, doctor, comprehensive doc, preview command |
| **Total** | **980** | |
