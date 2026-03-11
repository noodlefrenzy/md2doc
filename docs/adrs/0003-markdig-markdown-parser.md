---
agent-notes: { ctx: "ADR selecting Markdig as Markdown parser", deps: [docs/architecture.md], state: active, last: "archie@2026-03-11" }
---

# ADR-0003: Use Markdig as the Markdown Parser

## Status

Proposed

## Context

md2 needs a Markdown parser that supports CommonMark, GFM (tables, task lists, strikethrough, autolinks), and a set of extensions (YAML front matter, definition lists, attributes, admonitions, LaTeX math delimiters). The parser must produce a traversable AST suitable for transformation before emitting DOCX, and it must be actively maintained in the .NET ecosystem.

**Options evaluated:**

1. **Markdig** -- CommonMark-compliant, extensible, 20+ built-in extensions, used by Microsoft's own tooling (Windows Community Toolkit switched to Markdig internally). MIT-licensed. Created by Alexandre Mutel (xoofx), actively maintained. NuGet downloads: millions. Produces a rich AST (`MarkdownDocument`) with precise source positions and a data attachment system for custom metadata. Extension API supports custom block parsers, inline parsers, and renderers. No regex in the core parser. Thread-safe pipeline after construction.

2. **CommonMark.NET** -- Pure CommonMark implementation. Fewer extensions, smaller community. Would require writing most of the extensions we need (GFM tables, definition lists, admonitions, attributes) from scratch.

3. **Microsoft Community Toolkit MarkdownParser** -- Deprecated. Microsoft now recommends Markdig directly. The new MarkdownTextBlock control in the Toolkit Labs uses Markdig under the hood.

4. **Custom parser** -- Maximum control. Enormous effort. No practical justification given Markdig's capabilities.

## Decision

Use **Markdig** as the Markdown parser.

Configure the Markdig pipeline with these extensions:
- `UseAdvancedExtensions()` as a baseline (includes pipe tables, footnotes, definition lists, attributes, task lists, auto-identifiers, and more)
- `UseYamlFrontMatter()` for YAML front matter extraction
- Custom extension for admonition syntax (Markdig does not include admonitions natively; we will write a custom block parser following Markdig's extension API)
- `UseMathematics()` for LaTeX math delimiter recognition (`$...$` and `$$...$$`)

The Markdig AST (`MarkdownDocument`) will be used directly as our document representation throughout the pipeline. See ADR-0005 for the rationale against a custom intermediate representation.

## Consequences

### Positive

- Markdig covers the vast majority of our parsing needs out of the box. Minimal custom parser code.
- The extension API is well-documented and proven. Writing a custom admonition parser is a bounded task.
- Microsoft's own ecosystem relies on Markdig, which is a strong signal for long-term maintenance.
- The AST carries source positions, enabling precise error reporting ("line 42: unsupported LaTeX construct").
- Performance is excellent -- no regex in the hot path, object pooling, efficient string handling via `StringSlice`.
- MIT license with no transitive copyleft risk.

### Negative

- We take a dependency on a single maintainer's project (Alexandre Mutel). Bus factor is a concern, though the project has community contributors and Microsoft ecosystem adoption creates implicit maintenance pressure.
- Admonition support requires a custom extension (estimated 200-400 lines of code).
- If we later need AST transformations that Markdig's node types cannot represent cleanly, we may need to attach custom data via `MarkdownObject.SetData()` rather than having first-class typed nodes. This is workable but less elegant than a custom IR.

### Neutral

- Markdig is the de facto standard in .NET. Choosing anything else would require strong justification.
