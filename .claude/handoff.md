---
agent-notes: { ctx: "session handoff after Sprint 2 completion", deps: [CLAUDE.md, docs/plans/v1-implementation-plan.md], state: active, last: "grace@2026-03-11" }
---

# Session Handoff — md2doc

**Date:** 2026-03-11
**Last commit:** `63d258f` — feat(emit-docx,cli): Sprint 2
**Branch:** main
**Working tree:** Clean

## What Was Accomplished

### Sprint 1 (Issues 1-7) — DONE
- Parsing foundation: Markdig pipeline with CommonMark + GFM + extensions
- Custom admonition block parser
- YAML front matter extraction with DocumentMetadata mapping
- Core pipeline skeleton: ConversionPipeline (Parse → Transform → Emit)
- Typed AST extension methods (AstDataKeys, AstExtensions)
- YamlFrontMatterExtractor transform (order 010)
- 84 tests passing

### Sprint 2 (Issues 8-14) — DONE
- DocxEmitter with DocxAstVisitor and ParagraphBuilder
- Heading/paragraph/inline formatting (bold, italic, strikethrough, code, hyperlinks, line breaks)
- Page layout (A4, configurable margins, page numbers in footer)
- Document properties from front matter metadata
- CLI skeleton with System.CommandLine (convert command, -o, -q, -v, --help, --version)
- Hardcoded default ResolvedTheme with real font/color/size properties
- First E2E integration test
- 145 total tests passing

### Board Status
- Issues 1-14: **Done** on GitHub Projects board #14
- Issues 15-60: **Backlog**

## Architecture Notes

### Dependency Direction
- Md2.Core → Md2.Parsing (Core depends on Parsing)
- Md2.Emit.Docx → Md2.Core (Emitter depends on Core)
- Md2.Cli → Md2.Core, Md2.Emit.Docx, Md2.Parsing
- FrontMatterExtractor lives in Md2.Core assembly, namespace Md2.Parsing (avoids circular dep)
- ParserOptions lives in Md2.Parsing assembly

### Solution Structure
```
md2.sln
├── src/Md2.Core/         (pipeline, transforms, AST types)
├── src/Md2.Parsing/      (Markdig config, admonition parser, ParserOptions)
├── src/Md2.Emit.Docx/    (DOCX emitter, AST visitor, builders)
├── src/Md2.Cli/           (CLI entry point, convert command)
├── tests/Md2.Core.Tests/
├── tests/Md2.Parsing.Tests/
├── tests/Md2.Emit.Docx.Tests/
└── tests/Md2.Integration.Tests/
```

## What To Do Next

### Sprint 3: Tables, Images, and Lists (Issues 15-19)
**Wave 2 (Rich Elements)** — depends on Sprint 2 DocxEmitter infrastructure

**PROTOTYPE GATE: Table auto-sizing (Issue 15).** This is the highest-risk item.

Issues:
1. **Issue 15 (L) — GATE**: TableBuilder with auto-sizing column width heuristic
2. **Issue 16 (L)**: Table styling (borders, header row, alternating rows, cell padding, page split)
3. **Issue 17 (M)**: ImageBuilder with aspect ratio, scaling, alt text, missing file handling
4. **Issue 18 (L)**: ListBuilder with numbered, bulleted, nested, and task lists
5. **Issue 19 (M)**: SmartTypographyTransform (quotes, dashes, ellipsis, code-span exclusion)

**Recommended order:** 15 → 16 → 18 → 17 → 19 (table prototype gate first)

### Sprint 4: Code Blocks, Blockquotes, etc. (Issues 20-26)
Can partially overlap with Sprint 3 (different builders).

## Key Decisions Made
- ParserOptions in Md2.Parsing (not Md2.Core) to avoid circular deps
- FrontMatterExtractor in Md2.Core assembly with Md2.Parsing namespace
- Shouldly 4.2.1 for test assertions
- System.CommandLine 2.0.0-beta4 for CLI
- DocumentFormat.OpenXml 3.2.0 for DOCX generation
- Heading style IDs: "Heading1" through "Heading6" (Word built-in names)

## Proxy Decisions (Review Required)
None — human was present for Sprint 1 kickoff. Sprint 2 was completed while human was at dinner with Pat proxy available but no product questions arose.

## Tech Debt
- TD-001: Hardcoded default theme instead of real theme engine (planned, Sprint 6)
- YAML deserialization in FrontMatterExtractor has no depth/size limits (acceptable for CLI)
