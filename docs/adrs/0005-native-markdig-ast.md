---
agent-notes: { ctx: "ADR choosing Markdig native AST over custom IR", deps: [docs/architecture.md, docs/adrs/0003-markdig-markdown-parser.md], state: active, last: "archie@2026-03-11", key: ["typed extension methods over SetData are mandatory"] }
---

# ADR-0005: Use Markdig's Native AST (No Custom Intermediate Representation)

## Status

Proposed

## Context

The md2 pipeline has a transform phase between parsing and emitting. A central design question is whether transforms operate on Markdig's native `MarkdownDocument` AST or on a custom intermediate representation (IR) that we define.

**Options evaluated:**

1. **Use Markdig's native AST directly.** Transforms are visitors/walkers over `MarkdownDocument` and its node types (`HeadingBlock`, `ParagraphBlock`, `ListBlock`, `LinkInline`, etc.). Custom data is attached to nodes via `MarkdownObject.SetData(key, value)`. The emitter receives the same `MarkdownDocument` type.

2. **Define a custom IR.** Parse Markdig AST into our own document model (e.g., `Md2Document` with `Md2Heading`, `Md2Paragraph`, etc.). Transforms operate on the custom IR. The emitter receives the custom IR.

3. **Hybrid: Custom IR for PPTX only.** Use Markdig AST for the DOCX path. Introduce a `SlideDocument` IR only when PPTX support arrives, since slides have fundamentally different structure (content must be chunked into slides with layouts).

## Decision

Use **Markdig's native AST** (`MarkdownDocument`) as the document representation throughout the pipeline. Attach custom metadata using `MarkdownObject.SetData()` where needed (e.g., syntax highlight tokens, resolved math expressions, Mermaid image paths).

**Mandatory mitigation (added after Wei debate):** All `SetData`/`GetData` usage MUST go through strongly-typed extension methods. Direct `SetData(key, object)` calls are prohibited outside of the extension method implementations. This eliminates the stringly-typed API problem while preserving the decision to use Markdig's native AST.

```csharp
// Md2.Core/AstExtensions.cs
public static class AstExtensions
{
    // Syntax highlighting
    public static IReadOnlyList<SyntaxToken>? GetSyntaxTokens(this MarkdownObject node)
        => node.GetData(AstDataKeys.SyntaxTokens) as IReadOnlyList<SyntaxToken>;
    public static void SetSyntaxTokens(this MarkdownObject node, IReadOnlyList<SyntaxToken> tokens)
        => node.SetData(AstDataKeys.SyntaxTokens, tokens);

    // Mermaid diagram rendering
    public static string? GetMermaidImagePath(this MarkdownObject node)
        => node.GetData(AstDataKeys.MermaidImagePath) as string;
    public static void SetMermaidImagePath(this MarkdownObject node, string path)
        => node.SetData(AstDataKeys.MermaidImagePath, path);

    // Math (OMML XML)
    public static string? GetOmmlXml(this MarkdownObject node)
        => node.GetData(AstDataKeys.OmmlXml) as string;
    public static void SetOmmlXml(this MarkdownObject node, string omml)
        => node.SetData(AstDataKeys.OmmlXml, omml);

    // ... one pair per annotation type
}
```

**Transform contract documentation (added after Wei debate):** Each transform class documents which typed annotations it reads (preconditions) and which it writes (postconditions). This makes inter-transform ordering dependencies visible:

```csharp
/// <summary>
/// Reads: none
/// Writes: SetSyntaxTokens on FencedCodeBlock nodes
/// Order: 050 (must run before DOCX emitter)
/// </summary>
public class SyntaxHighlightAnnotator : IAstTransform { ... }
```

If PPTX requires a fundamentally different document structure in v2, introduce a targeted `SlideDocument` model at that point -- but this is a v2 decision, not a v1 decision. Do not over-engineer for a speculative need.

**Rationale:**

- A custom IR duplicates every Markdig node type (there are 30+) with no functional benefit for v1.
- The Markdig AST already carries source positions, trivia, and extensible metadata -- all things we would need to replicate.
- Transforms that add information (syntax highlighting tokens, math OMML, Mermaid image references) work naturally via typed extension methods over `SetData()`.
- Transforms that restructure (TOC generation, cover page insertion) work by modifying the `MarkdownDocument` tree directly, which Markdig's mutable AST supports.
- The existing `markdig.docx` project validates that a Markdig AST can be walked directly to produce Open XML output.

## Consequences

### Positive

- No translation layer between parse and emit. Simpler code, fewer bugs.
- All Markdig extension node types (tables, footnotes, definition lists, etc.) are immediately available without mapping.
- Source position information flows through the entire pipeline for error reporting.
- Faster development: we start emitting DOCX immediately without building an IR first.

### Negative

- **Coupling to Markdig.** If we ever replace Markdig, the transform and emitter code must change. This is an acceptable risk given Markdig's dominance and our use of `SetData()` only for our own extensions.
- **Custom metadata requires discipline.** The underlying `SetData()` uses `object` keys and values. The mandatory typed extension method layer (see Decision section) provides compile-time safety, but developers must use the extension methods rather than calling `SetData()` directly. Code review must enforce this.
- **Mutable AST.** Transforms modify the AST in place. Order of transforms matters. A transform that runs out of order could see inconsistent state. Mitigation: transforms declare an explicit `Order` property and the pipeline enforces sequencing.
- **PPTX may force a rethink.** Slide chunking is a structural transformation that the Markdig AST was not designed for. We may need to introduce a slide-specific model in v2.

### Neutral

- This decision can be revisited if PPTX support reveals that a shared IR is necessary. The cost of introducing an IR later is significant but mechanical: the emitter's visitor methods access structural properties (heading level, code fence info, paragraph children) that any IR would replicate. The method bodies would change types but not logic. It is a refactor, not a redesign -- but it is a non-trivial refactor across dozens of visitor methods.
