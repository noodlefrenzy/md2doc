---
agent-notes: { ctx: "ADR selecting TextMateSharp for syntax highlighting", deps: [docs/architecture.md], state: active, last: "archie@2026-03-11" }
---

# ADR-0007: Use TextMateSharp for Syntax Highlighting

## Status

Proposed

## Context

md2 must render fenced code blocks with syntax highlighting in DOCX output. Unlike HTML where CSS handles coloring, DOCX requires explicit run-level formatting (font color, bold, italic) on each token. We need a library that tokenizes source code into classified spans with color/style information.

**Options evaluated:**

1. **TextMateSharp** -- A C# port of `microsoft/vscode-textmate`. Uses the same TextMate grammar definitions (.tmLanguage) that VS Code uses. Supports 30+ languages out of the box with bundled grammars. Includes 20+ VS Code themes (Dark+, Light+, Monokai, Solarized, Dracula, etc.). MIT-licensed. Maintained by the AvaloniaEdit community. NuGet package `TextMateSharp` v2.0.3.

2. **ColorCode-Universal** -- Community Toolkit port. Supports fewer languages than TextMateSharp. Outputs to HTML or UWP RichTextBlock, not to generic token lists. We would need to intercept the formatting pipeline to extract tokens. Less active development.

3. **Smdn.LibHighlightSharp** -- .NET wrapper around Andre Simon's `highlight` C library. Requires native binary distribution. Cross-platform native interop adds packaging complexity.

4. **Render code blocks as images via Playwright.** Use a code-highlighting JS library (Shiki, Prism) in Chromium to render code to PNG. Overkill for syntax highlighting alone and produces non-selectable text in DOCX.

5. **Custom tokenizer per language.** Maximum control, enormous effort. Not practical for 30+ languages.

## Decision

Use **TextMateSharp** for syntax highlighting.

**Architecture:**

1. The `SyntaxHighlightAnnotator` transform (Md2.Highlight) processes each `FencedCodeBlock` in the AST.
2. TextMateSharp tokenizes the code content using the appropriate grammar (detected from the code fence info string, e.g., ` ```csharp `).
3. Tokens are resolved against a theme (mapping TextMate scopes to colors).
4. The token list is attached to the AST node via `SetData(AstDataKeys.SyntaxTokens, tokens)`.
5. The DOCX emitter reads the token list and creates OpenXml `Run` elements with appropriate `RunProperties` (color, bold, italic).

**Theme mapping:** The YAML theme file can specify a `codeTheme` property that maps to a TextMateSharp theme name (e.g., `"Dark+"`, `"Monokai"`). Alternatively, the theme's `colors.codeBg` and related properties drive a light/dark auto-selection.

## Consequences

### Positive

- VS Code-quality syntax highlighting. Users get familiar, accurate colorization.
- 30+ languages supported immediately via bundled TextMate grammars.
- Theme-aware highlighting. Code block colors can adapt to the document's overall style.
- MIT-licensed, no native dependencies (pure C# with an Oniguruma regex wrapper).
- Proven technology -- the same grammars power VS Code, the world's most popular editor.

### Negative

- **Oniguruma dependency.** TextMateSharp wraps the Oniguruma regex library (native binary). This means platform-specific native binaries must be bundled. The NuGet package handles this for common platforms (Windows x64, Linux x64, macOS x64/arm64), but exotic platforms may not be covered.
- **Grammar loading cost.** First tokenization for a language loads and parses its .tmLanguage grammar. This is a one-time cost per language per run, but could add ~50-100ms per new language in a document with many different code blocks.
- **Scope-to-color mapping complexity.** TextMate scopes are hierarchical (e.g., `keyword.control.flow.csharp`). Mapping these to DOCX run formatting requires a theme resolution layer. We must implement this mapping ourselves.

### Neutral

- TextMateSharp's grammars may lag behind VS Code's latest grammar updates. The impact is minimal for stable languages but could matter for newer languages or syntax features.
- We could replace TextMateSharp with any other tokenizer in the future since the integration point is a simple token list attached to the AST. The emitter does not know or care what produced the tokens.
