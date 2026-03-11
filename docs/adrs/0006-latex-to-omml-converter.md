---
agent-notes: { ctx: "ADR for LaTeX-to-OMML via MathML intermediate path", deps: [docs/architecture.md, docs/adrs/0004-open-xml-sdk.md, docs/adrs/0008-playwright-mermaid-rendering.md], state: active, last: "archie@2026-03-11", key: ["revised after Wei debate: MathML path replaces direct converter"] }
---

# ADR-0006: LaTeX-to-OMML via MathML Intermediate Path (KaTeX + MML2OMML.xsl)

## Status

Proposed (revised after Wei debate -- replaces original "Custom LaTeX-to-OMML Converter" proposal)

## Context

md2 needs to convert LaTeX math expressions (inline `$...$` and display `$$...$$`) into DOCX-native math objects. The target format is OMML (Office Math Markup Language), the native math format in OOXML. OMML renders natively in Word without plugins or images, supports editing, and respects document styles.

**Options evaluated:**

1. **Custom C# LaTeX-to-OMML converter.** Write a parser for a pragmatic LaTeX math subset and emit OMML XML directly. Zero C# precedent. pandoc's `texmath` (Haskell) is 11,000+ lines for this task. Estimated 1000-2000 lines for a subset, with significant fidelity gaps.

2. **Shell out to pandoc's `texmath`.** The gold standard, but requires pandoc installed on the user's machine. Adds a Haskell runtime dependency. Violates local-only, air-gap philosophy.

3. **CSharpMath library.** C# LaTeX renderer (iosMath port). Renders to images via SkiaSharp. Does NOT produce OMML or MathML. Last release 0.5.1, limited maintenance.

4. **Render math as PNG via Playwright + KaTeX.** Use the Chromium instance to render LaTeX to high-resolution PNG. Embeds as image in DOCX. Non-editable, does not scale with text, does not respect document fonts.

5. **LaTeX -> MathML -> OMML via KaTeX + MML2OMML.xsl.** Use KaTeX (via Playwright) to convert LaTeX to MathML. Apply Microsoft's `MML2OMML.xsl` XSLT stylesheet via `System.Xml.Xsl.XslCompiledTransform` (built into .NET) to convert MathML to OMML. Insert OMML into the DOCX.

## Decision

**Primary path: LaTeX -> MathML -> OMML** (option 5).

The conversion pipeline:

```
LaTeX string (from Markdown)
  --> KaTeX (via Playwright, MathML output mode)
  --> MathML XML string
  --> MML2OMML.xsl (via System.Xml.Xsl.XslCompiledTransform)
  --> OMML XML elements
  --> inserted into WordprocessingDocument
```

**Step 1: LaTeX to MathML via KaTeX.** KaTeX natively outputs MathML alongside its HTML rendering. We invoke KaTeX in the same Playwright/Chromium instance used for Mermaid rendering. KaTeX supports a broad LaTeX subset including all common constructs: fractions, superscripts/subscripts, Greek letters, big operators with limits, matrices, aligned equations, cases environments, accents, roots, delimiters, and most `amsmath` environments. This eliminates the need to write any LaTeX parser.

**Step 2: MathML to OMML via MML2OMML.xsl.** Microsoft ships this XSLT with Office (typically at `C:\Program Files\Microsoft Office\root\Office16\MML2OMML.xsl`). The stylesheet is widely redistributed in open-source projects (transpect/docx_modify-lib, meTypeset, libtex2omml, among others) and its origins trace to David Carlisle's W3C work. We bundle a copy with appropriate attribution. `XslCompiledTransform` is a built-in .NET BCL class -- this is not an external dependency.

**Step 3: OMML insertion.** The resulting OMML XML elements are deserialized into Open XML SDK types and inserted into the document body. Inline math (`$...$`) goes into a `Run` within a `Paragraph`. Display math (`$$...$$`) goes into its own `Paragraph` with display math properties.

**Fallback path: PNG rendering** for the rare case where KaTeX cannot handle an expression (highly custom LaTeX macros, TikZ, etc.). KaTeX's error handling clearly indicates unsupported expressions. The PNG path uses KaTeX's HTML rendering at high DPI, same as the original proposal.

**Why this changed from the original proposal:** Wei's debate challenge (Critical severity) correctly identified that a direct LaTeX-to-OMML converter has zero C# precedent and that the MathML intermediate path is battle-tested. The original ADR dismissed the MathML path as "adds complexity (two conversion steps, XSLT dependency)" -- this was a poor evaluation. Two proven steps composed together are far less risky than one novel step. `XslCompiledTransform` is not an external dependency. And the PNG fallback being "a fundamentally different product" (non-editable, non-scaling) means it cannot be the expected path for anything beyond rare edge cases.

## Consequences

### Positive

- **Proven components.** KaTeX is mature (millions of users). MML2OMML.xsl is Microsoft's own conversion. Neither is novel.
- **Broad LaTeX coverage.** KaTeX supports significantly more LaTeX than any custom subset we would define. `amsmath` environments, aligned equations, and cases environments all work out of the box.
- **Zero custom parsing.** We write no LaTeX parser. KaTeX handles all parsing and semantic interpretation.
- **OMML output is editable in Word.** Users can modify equations after conversion. The output respects document fonts and scales with text.
- **Reuses existing infrastructure.** The Playwright/Chromium instance is already required for Mermaid (ADR-0008) and is being established as the rendering backend for math as well. Incremental cost is minimal.
- **`XslCompiledTransform` is built into .NET.** No additional NuGet dependency for the XSLT step.

### Negative

- **Playwright/Chromium dependency for math.** Unlike the original proposal (which claimed zero external dependencies for common math), this approach requires Chromium to be installed even for documents that contain only simple math and no Mermaid diagrams. This is a meaningful trade-off for users who want math support in an air-gapped environment -- they must download Chromium once.
- **Latency.** Each math expression requires a Playwright round-trip (inject LaTeX, extract MathML). For documents with many inline math expressions (e.g., 50+ `$x$` occurrences), this could be slow. **Mitigation:** Batch all math expressions into a single page render (inject all LaTeX at once, extract all MathML in one round-trip). This amortizes browser overhead.
- **MML2OMML.xsl licensing.** The file originates from Microsoft and is widely redistributed, but its standalone licensing status is not formally documented by Microsoft. We will include it with attribution and note the provenance. **Mitigation:** If licensing proves problematic, the XSLT is a well-understood transformation that could be reimplemented from the MathML and OMML specs.
- **KaTeX is not LaTeX.** KaTeX supports a large but not complete subset of LaTeX. Expressions using custom macros, TikZ, or obscure packages will fail. **Mitigation:** KaTeX error output clearly identifies unsupported expressions; we fall back to PNG for these cases with a warning.

### Neutral

- The original "direct converter" approach is not abandoned but demoted to a potential future optimization. If Playwright round-trip latency for inline math proves to be a bottleneck (measured, not assumed), a simple direct converter for basic expressions (`x^2`, `\alpha`, `\frac{a}{b}`) could skip the Playwright step. This is a performance optimization, not a v1 requirement.
- KaTeX JS (~1MB minified) is bundled alongside Mermaid JS in the `Md2.Diagrams` assembly. Both are loaded into the same Chromium page.
- This approach handles both inline and display math identically (same pipeline, different OMML insertion point).
