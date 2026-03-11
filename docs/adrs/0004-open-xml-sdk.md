---
agent-notes: { ctx: "ADR selecting Open XML SDK for DOCX/PPTX generation", deps: [docs/architecture.md], state: active, last: "archie@2026-03-11", key: ["table auto-sizing prototype gate: 5 days"] }
---

# ADR-0004: Use Open XML SDK for DOCX/PPTX Generation

## Status

Proposed

## Context

md2 needs to produce DOCX files (v1) and PPTX files (v2) with fine-grained control over styles, typography, page layout, tables, images, math, and document structure. The generation library must support:
- Creating documents from scratch or from a template
- Full style system manipulation (named styles, direct formatting, style inheritance)
- Table properties (column widths, cell merging, borders, shading)
- Image embedding with aspect ratio control
- OOXML math objects
- Headers, footers, page numbers, TOC fields
- Running on Windows and Linux without COM interop or Office installation

**Options evaluated:**

1. **Open XML SDK** (DocumentFormat.OpenXml, v3.x) -- Microsoft's official library for reading/writing OOXML formats. .NET Foundation project. Provides strongly-typed classes for every Open XML element. Low-level: you construct XML elements programmatically. No layout engine (you calculate widths, positions yourself). Supports DOCX, XLSX, PPTX. MIT-licensed. Actively maintained (v3.4.1 released December 2025 with Q3 2025 Office schema updates).

2. **GemBox.Document** -- Commercial library with a higher-level API. Handles layout, pagination, and rendering. Supports DOCX, PDF, HTML, RTF. Free tier limits to 20 paragraphs. Production use requires a paid license (~$890+). Would significantly reduce code for table sizing and page layout.

3. **IronWord** -- Commercial library. Simpler API than Open XML SDK. Supports .NET 6-8. Less mature than GemBox. Paid license required.

4. **DocX (Xceed)** -- Open-source wrapper around Open XML. Simplifies common operations. Less control over low-level styling. Development has slowed.

5. **NPOI** -- Port of Apache POI. Supports DOCX and XLSX. Mixed API quality. Apache 2.0 license. Less precise control over OOXML features than the official SDK.

## Decision

Use **Open XML SDK** (DocumentFormat.OpenXml v3.x) as the DOCX and PPTX generation library.

**Rationale:**

- **Control.** We need pixel-level control over styles, typography, and document structure to produce output "noticeably superior to pandoc." Higher-level libraries abstract away the very details we need to control.
- **No license cost.** The project is open-source for personal and eventual community use. Commercial library costs are not justified.
- **Single library for DOCX + PPTX.** Open XML SDK handles both formats. No need for a second library when PPTX support arrives in v2.
- **Active maintenance.** Microsoft and the .NET Foundation maintain it. Schema updates track Office releases.
- **Full OOXML access.** We can implement any OOXML feature that Word supports, including OMML math, complex table properties, and advanced style inheritance.

The trade-off is that Open XML SDK is low-level. We will build our own abstraction layers for common patterns (style application, table sizing, image embedding) in `Md2.Emit.Docx`.

**Prototype gate (added after Wei debate):** The absence of a layout engine is the single largest implementation risk. Table auto-sizing -- calculating column widths from content without a rendering engine -- is the hardest subproblem. Before committing to full implementation:

- **Time-box:** 5 working days to prototype table auto-sizing.
- **Test cases:** (a) simple uniform 3-column table, (b) table with varying content lengths (one cell has a paragraph, another has one word), (c) table with a styled header row, (d) table with one very long cell forcing text wrap.
- **Pass criteria:** Output opened in Word shows reasonable column widths (within 10% of what a human would set) for all four test cases.
- **Fail action:** If 5 days cannot meet pass criteria, reassess. Options: (a) accept "good enough" heuristic and iterate post-v1, (b) evaluate GemBox.Document ($890 license) for its layout engine while retaining Open XML SDK for areas where GemBox lacks control (custom styles, OMML math, advanced formatting), (c) hybrid approach using GemBox for table generation and Open XML SDK for everything else.

**Key OOXML spec sections requiring careful implementation:**
- ISO/IEC 29500-1:2016, clause 17.4 (Tables) -- table grid, cell merging, auto-fit algorithms
- ISO/IEC 29500-1:2016, clause 17.3 (Paragraphs) -- paragraph properties, spacing, keep-with-next for widow/orphan control
- ISO/IEC 29500-1:2016, clause 17.7 (Styles) -- style hierarchy, inheritance, latent styles
- ISO/IEC 29500-1:2016, clause 17.9 (Numbering) -- list numbering definitions and levels
- ISO/IEC 29500-1:2016, clause 22.1 (Math/OMML) -- math element construction

## Consequences

### Positive

- Maximum control over output quality. Every OOXML property is accessible.
- No commercial license dependency. MIT-licensed.
- Covers both DOCX and PPTX in a single dependency.
- Strong typing catches many errors at compile time (e.g., wrong element nesting).
- Large community and documentation (Microsoft Learn, Stack Overflow).

### Negative

- **Significant boilerplate.** Constructing Open XML elements is verbose. A simple styled paragraph is 10-20 lines of code. We mitigate this with builder abstractions in the emitter layer.
- **No layout engine -- the single largest implementation risk.** Table column widths, page breaks, and content flow must be calculated manually. This is the most tedious and error-prone part of the project. Table auto-sizing has a 5-day prototype gate (see above). GemBox.Document is the named fallback if the prototype reveals that the overall OOXML construction complexity makes the project timeline untenable.
- **OOXML spec complexity.** The Open XML specification is enormous (~6000 pages). Correct behavior requires careful reading of the spec, not just the SDK docs.
- **Testing requires opening the output.** Unit tests can assert XML structure, but visual correctness requires opening in Word or using document comparison tools.

### Neutral

- The existing `markdig.docx` project demonstrates that a Markdig-to-Open-XML-SDK renderer is viable, though it covers only basic elements. We will build our own emitter with much broader coverage, but the existence of that project validates the approach.
