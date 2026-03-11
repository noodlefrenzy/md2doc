---
agent-notes:
  ctx: "v1 acceptance criteria, feature areas, implementation waves"
  deps: [docs/product-context.md, docs/architecture.md, docs/tracking/2026-03-11-md2doc-discovery.md, docs/tracking/2026-03-11-md2doc-adr-debate.md]
  state: active
  last: "pat@2026-03-11"
  key: ["4 waves", "PPTX is v2", "variable interpolation deferred", "table auto-sizing has 5-day gate"]
---

# md2 v1 -- Acceptance Criteria

**Author:** Pat (product/program)
**Date:** 2026-03-11
**Status:** Proposed -- pending team review

---

## Scope Summary

### v1 includes

- DOCX output (the entire conversion pipeline for Markdown to polished .docx)
- 5 built-in style presets (default, technical, corporate, academic, minimal)
- YAML theme files (plain values, no `${...}` interpolation)
- 4-layer style cascade (CLI overrides > YAML theme > preset > template)
- Custom DOCX template support with cascade-and-warn
- Theme management commands (extract, validate, list, resolve)
- Diagnostic command (`md2 doctor`)
- Preview with hot-reload
- All P0 and P1 features from Dani's tiered model
- P2 features: built-in presets, multi-file concatenation, image captions

### v1 explicitly excludes

- PPTX output (v2 -- the `IFormatEmitter` seam exists but `Md2.Emit.Pptx` is stubbed)
- `${...}` variable interpolation in theme YAML (deferred post-v1, plain literal values only)
- `--template-password` flag for password-protected DOCX templates (v2)
- DOCX accessibility tagging (v2 -- tracked as P2 delighter, not in v1 scope)
- Internal cross-references with page numbers (v2 -- requires Word field codes, complex)
- SVG fallback for Mermaid when Playwright is unavailable (noted, not committed)
- Direct LaTeX-to-OMML fast path for simple expressions (future optimization)
- MARP-style slide breaks or any PPTX-related transforms

---

## Implementation Waves

Work is grouped by dependency. Each wave can be delivered as a unit. The human prefers complete delivery, but within v1 the natural layering is:

| Wave | Name | What It Delivers | Depends On |
|------|------|-------------------|------------|
| **1** | Foundation | Parser, pipeline skeleton, theme engine, basic DOCX output (headings, paragraphs, lists, links, images, inline code) | Nothing |
| **2** | Rich Content | Tables, code blocks with syntax highlighting, blockquotes, footnotes, admonitions, definition lists, math, Mermaid diagrams | Wave 1 |
| **3** | Polish and UX | TOC, cover page, smart typography, style presets, template cascade, theme commands, `md2 doctor`, preview | Waves 1-2 |
| **4** | Integration and Ship | Multi-file concatenation, end-to-end testing, preset visual regression, CLI polish, documentation | Waves 1-3 |

---

## Feature Area 1: Markdown Parsing

### US-1.1: Extended Markdown Parsing

> As a technical author, I want md2 to parse all Markdown syntax I use so that no content is lost or mangled in conversion.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-1.1.1 | CommonMark block elements (paragraphs, headings 1-6, block quotes, ordered/unordered lists, fenced/indented code blocks, thematic breaks, HTML blocks) are parsed into Markdig AST nodes. | P0 | Unit test: parse representative Markdown containing each element type. Assert each produces the correct Markdig AST node type. |
| AC-1.1.2 | GFM extensions (tables, strikethrough, autolinks, task lists) are parsed. | P0 | Unit test: parse GFM-specific syntax. Assert correct AST node types for each extension. |
| AC-1.1.3 | Admonition blocks (`:::note`, `:::warning`, `:::tip`, `:::important`, `:::caution`) are parsed into typed `AdmonitionBlock` nodes with the admonition type preserved. | P1 | Unit test: parse each admonition type. Assert `AdmonitionBlock.Type` matches. |
| AC-1.1.4 | Definition lists (`term\n: definition`) are parsed. | P1 | Unit test: parse definition list syntax. Assert correct AST structure. |
| AC-1.1.5 | Generic attributes (`{.class #id key=value}`) are parsed and attached to the preceding element's AST node. | P1 | Unit test: parse element with attributes. Assert attributes are accessible via Markdig's attribute API. |
| AC-1.1.6 | LaTeX math expressions (inline `$...$` and display `$$...$$`) are parsed and identified as math nodes in the AST. | P1 | Unit test: parse inline and display math. Assert correct AST annotation. |
| AC-1.1.7 | Mermaid code blocks (fenced code with language `mermaid`) are identified for downstream rendering. | P1 | Unit test: parse fenced code block with `mermaid` info string. Assert identification. |
| AC-1.1.8 | Footnote references (`[^1]`) and footnote definitions (`[^1]: text`) are parsed. | P1 | Unit test: parse footnotes. Assert reference-definition linkage in AST. |
| AC-1.1.9 | Nested structures (list inside blockquote, code block inside list, table inside blockquote) are parsed correctly. | P0 | Unit test: parse 5+ nested structure combinations. Assert correct parent-child relationships. |

### US-1.2: YAML Front Matter Extraction

> As a user, I want document metadata (title, author, date, abstract) extracted from YAML front matter so it can populate the DOCX document properties and cover page.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-1.2.1 | YAML front matter delimited by `---` is extracted before parsing and does not appear in the document body. | P0 | Unit test: Markdown with front matter. Assert front matter is in `DocumentMetadata`, not in AST body nodes. |
| AC-1.2.2 | Standard fields (`title`, `author`, `date`, `abstract`, `subject`, `keywords`) are mapped to `DocumentMetadata` properties. | P0 | Unit test: front matter with each field. Assert correct property values in `DocumentMetadata`. |
| AC-1.2.3 | Unknown front matter fields are preserved in a `Dictionary<string, object>` on `DocumentMetadata` for template use. | P1 | Unit test: front matter with custom fields. Assert they exist in the extras dictionary. |
| AC-1.2.4 | Malformed YAML front matter produces a clear error message with the line number of the parse error. | P0 | Unit test: malformed YAML. Assert error message contains "front matter" and a line number. |

---

## Feature Area 2: AST Transforms

### US-2.1: Smart Typography

> As a user, I want typographic improvements applied automatically so the output reads like a professionally typeset document.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-2.1.1 | Straight quotes (`"` and `'`) are converted to curly/smart quotes (open and close). | P1 | Unit test: input with straight quotes in various positions (beginning of word, end of word, nested). Assert correct Unicode curly quote characters in transformed AST. |
| AC-2.1.2 | `--` is converted to en-dash. `---` is converted to em-dash. | P1 | Unit test: Assert Unicode en-dash and em-dash characters. |
| AC-2.1.3 | `...` is converted to a single ellipsis character. | P1 | Unit test: Assert Unicode ellipsis character. |
| AC-2.1.4 | Smart typography does NOT modify content inside code spans or code blocks. | P1 | Unit test: straight quotes inside backticks remain straight. |

### US-2.2: Table of Contents Generation

> As a user, I want an auto-generated TOC so I do not have to maintain one manually.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-2.2.1 | When `--toc` is specified, a table of contents is generated from heading nodes in the AST. | P1 | Integration test: Markdown with H1-H4 headings + `--toc`. Assert DOCX contains a TOC section before the first body heading. |
| AC-2.2.2 | TOC entries include heading text and are hyperlinked to the heading's position in the document. | P1 | Integration test: click a TOC entry in the output DOCX and verify it navigates to the heading (inspect bookmark/hyperlink structure in OOXML). |
| AC-2.2.3 | TOC depth defaults to 3 levels (H1-H3). A `--toc-depth <N>` flag controls this (1-6). | P1 | Unit test: AST with H1-H6. Assert only H1-H3 appear in generated TOC structure. Repeat with `--toc-depth 4`. |
| AC-2.2.4 | TOC is styled according to the resolved theme (TOC-specific styles: indentation per level, font, spacing). | P1 | Integration test: inspect DOCX TOC paragraph styles against resolved theme. |

### US-2.3: Cover Page Generation

> As a user, I want a title/cover page generated from front matter so reports look complete.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-2.3.1 | When `--cover` is specified and front matter contains `title`, a cover page is generated as the first page of the DOCX. | P1 | Integration test: Markdown with `title` in front matter + `--cover`. Assert DOCX first section contains the title text with cover page styling. |
| AC-2.3.2 | Cover page displays: title (required), subtitle (if present), author (if present), date (if present), abstract (if present). | P1 | Integration test: front matter with all fields. Assert each appears on the cover page. |
| AC-2.3.3 | Cover page is followed by a section break (next page) so body content starts on a new page. | P1 | Integration test: inspect OOXML for section break after cover page content. |
| AC-2.3.4 | If `--cover` is specified but front matter has no `title`, emit a warning and skip cover page generation. | P1 | Unit test: assert warning message. Assert no cover page in output. |

### US-2.4: Cross-Reference Linking

> As a user, I want `[text](#heading-slug)` links resolved to bookmarks in the DOCX so internal navigation works.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-2.4.1 | Headings are assigned bookmark IDs based on their slug (lowercased, hyphenated text). | P1 | Integration test: DOCX heading has a `BookmarkStart`/`BookmarkEnd` pair with the expected ID. |
| AC-2.4.2 | Internal links (`[text](#slug)`) become hyperlinks pointing to the heading bookmark. | P1 | Integration test: hyperlink `Anchor` matches the heading bookmark ID. |
| AC-2.4.3 | Duplicate heading text produces unique bookmark IDs (e.g., `heading`, `heading-1`, `heading-2`). | P1 | Unit test: three headings with identical text. Assert three distinct bookmark IDs. |

---

## Feature Area 3: DOCX Emission -- Core Elements

### US-3.1: Headings

> As a user, I want headings rendered with proper Word heading styles so the document has navigable structure.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-3.1.1 | Heading levels 1-6 map to Word built-in heading styles (Heading 1 through Heading 6). | P0 | Integration test: Markdown with H1-H6. Open DOCX, assert each paragraph's `ParagraphStyleId` matches `Heading1` through `Heading6`. |
| AC-3.1.2 | Heading font, size, color, spacing before/after, and bold/italic are controlled by the resolved theme. | P0 | Integration test: convert with a custom theme specifying non-default heading styles. Assert DOCX run properties match the theme. |
| AC-3.1.3 | Headings appear in Word's Navigation Pane (document outline). | P0 | Integration test: assert heading paragraphs have `OutlineLevel` set in OOXML. |

### US-3.2: Body Text and Inline Formatting

> As a user, I want body text with correct inline formatting so the document is readable and styled.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-3.2.1 | Body paragraphs use the theme's body font, size, color, and line spacing. | P0 | Integration test: assert DOCX paragraph and run properties match resolved theme body settings. |
| AC-3.2.2 | Bold (`**text**`), italic (`*text*`), bold-italic (`***text***`), and strikethrough (`~~text~~`) produce correct OOXML run properties. | P0 | Integration test: inline-formatted text. Assert `Bold`, `Italic`, `Strike` run properties. |
| AC-3.2.3 | Inline code (`` `code` ``) is rendered in the theme's mono font with a background shading. | P0 | Integration test: assert run has mono font and shading element. |
| AC-3.2.4 | Hyperlinks (`[text](url)`) produce clickable hyperlinks in the DOCX with the URL preserved. | P0 | Integration test: assert `Hyperlink` element with correct `Uri`. |
| AC-3.2.5 | Line breaks within a paragraph (two trailing spaces or `\`) produce a OOXML `Break` element, not a new paragraph. | P0 | Integration test: assert `Break` within same paragraph, not separate `Paragraph` elements. |
| AC-3.2.6 | Widow and orphan control is enabled for body paragraphs. | P0 | Integration test: assert `WidowControl` is present in paragraph properties. |

### US-3.3: Lists

> As a user, I want lists rendered with correct nesting, numbering, and bullet styles.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-3.3.1 | Unordered lists produce bulleted paragraphs with appropriate bullet characters per nesting level. | P0 | Integration test: 3-level nested unordered list. Assert each level has a distinct `NumberingId` and `NumberingLevelReference`. |
| AC-3.3.2 | Ordered lists produce numbered paragraphs with sequential numbering that restarts per list. | P0 | Integration test: two separate ordered lists. Assert numbering restarts for the second list. |
| AC-3.3.3 | Nested lists (unordered inside ordered, ordered inside unordered) maintain correct indentation and numbering at each level. | P0 | Integration test: mixed nested list. Assert correct indentation and numbering type per level. |
| AC-3.3.4 | Task list items (`- [x]` and `- [ ]`) render with a checkbox character (checked/unchecked) as the bullet. | P0 | Integration test: assert task list items have checkbox Unicode characters or form field controls. |
| AC-3.3.5 | List items containing multiple paragraphs, code blocks, or other block elements render correctly with proper indentation. | P0 | Integration test: list item with a paragraph followed by a code block. Assert both are indented to the list level. |

### US-3.4: Images

> As a user, I want images embedded in the DOCX with correct sizing and aspect ratio.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-3.4.1 | Images referenced by relative path (`![alt](./img.png)`) are embedded in the DOCX as inline images. | P0 | Integration test: DOCX contains the image in the media part. Image `src` resolved relative to the input Markdown file's directory. |
| AC-3.4.2 | Image aspect ratio is preserved. Images wider than the page content area are scaled down to fit. | P0 | Integration test: embed a 3000px wide image. Assert DOCX image width equals page content width (page width minus margins) and height is proportionally scaled. |
| AC-3.4.3 | Alt text from `![alt text](url)` is set as the image's description property in OOXML (for accessibility). | P0 | Integration test: assert `DocProperties` on the image's `Drawing` element contains the alt text. |
| AC-3.4.4 | Images referenced by absolute path work. Missing image files produce a warning (not a crash) and a placeholder in the output. | P0 | Integration test: Markdown referencing a nonexistent image. Assert warning on stderr and a visible placeholder (e.g., `[Image not found: path]`) in the DOCX. |
| AC-3.4.5 | Image captions (text on the line after the image, or using a convention like `![alt](url "caption")`) produce a styled caption paragraph below the image. | P2 | Integration test: image with title attribute. Assert a paragraph with caption style follows the image. |

### US-3.5: Page Layout

> As a user, I want control over page size, margins, headers, footers, and page numbers.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-3.5.1 | Default page size is A4. Configurable via theme YAML (`docx.pageSize`). Supported values at minimum: A4, Letter, A3, Legal. | P0 | Integration test: convert with `pageSize: Letter`. Assert OOXML `SectionProperties` page size matches Letter dimensions. |
| AC-3.5.2 | Margins are configurable via theme YAML (`docx.margins: { top, bottom, left, right }`). Defaults: top/bottom 1in, left/right 1.25in. | P0 | Integration test: custom margins. Assert OOXML `PageMargin` values match. |
| AC-3.5.3 | Page numbers are included in the footer. Style is configurable (`"Page {page} of {pages}"` or just `"{page}"`). | P0 | Integration test: assert footer contains `PAGE` and `NUMPAGES` field codes matching the theme's footer style pattern. |
| AC-3.5.4 | Header text is configurable via theme YAML (`docx.header.text`). Header appears on all pages except the cover page (if present). | P1 | Integration test: theme with header text. Assert DOCX header part contains the text. If cover page present, assert first-page header is different (empty or cover-specific). |

---

## Feature Area 4: DOCX Emission -- Rich Elements

### US-4.1: Tables

> As a user, I want tables that look professional: auto-sized columns, header distinction, alternating rows, cross-page splitting.

**NOTE: Table auto-sizing has a 5-day prototype gate (per ADR-0004 debate). If the prototype cannot handle the four test cases (uniform, varying lengths, header row, one very long cell) in 5 days, the approach is reassessed.**

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-4.1.1 | GFM tables render as Word tables with borders matching the theme's table style. | P0 | Integration test: simple 3-column table. Assert OOXML `Table` element with `TableBorders` matching theme. |
| AC-4.1.2 | Header row is visually distinct (background color, bold text) per theme definition. | P0 | Integration test: assert first row has `TableCellShading` matching `table.headerBg` and runs are bold. |
| AC-4.1.3 | Alternating row shading is applied per theme (`table.alternateRowBg`). | P0 | Integration test: 6-row table. Assert even/odd row shading differs. |
| AC-4.1.4 | Column widths are auto-sized based on content. Columns with more content get proportionally more width. The total table width equals the page content width. | P0 | Integration test: table with columns of varying content length. Assert column widths sum to page content width and longer-content columns are wider. Tolerance: widths are within 15% of an ideal proportional distribution. |
| AC-4.1.5 | Tables split across pages when content exceeds one page. The header row repeats on each page. | P0 | Integration test: table with 50+ rows. Assert `TableRowProperties.TableHeader` is set on the first row (enables Word's header repeat). |
| AC-4.1.6 | Table borders are thin (0.5pt per theme default). Border color is configurable via theme. | P0 | Integration test: assert `TableBorders` width and color match theme values. |
| AC-4.1.7 | Cell padding is consistent and produces readable spacing between cell borders and text. | P0 | Integration test: assert `TableCellMargin` values are set (non-zero). |

### US-4.2: Code Blocks with Syntax Highlighting

> As a user, I want code blocks with monospace font, background shading, and syntax highlighting so code is easy to read.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-4.2.1 | Fenced code blocks render in the theme's mono font with a background fill color. | P0 | Integration test: assert code paragraph runs use the mono font and the paragraph has shading matching `code.background`. |
| AC-4.2.2 | When a language is specified (e.g., ` ```python `), tokens are syntax-highlighted with colors from TextMateSharp. | P0 | Integration test: Python code block with keywords, strings, comments. Assert runs have distinct foreground colors (at least 3 different colors for different token types). |
| AC-4.2.3 | Code blocks without a language specifier render with mono font and background but no syntax highlighting (plain text). | P0 | Integration test: code block without language. Assert all runs use the same foreground color. |
| AC-4.2.4 | At least 20 common languages are supported for syntax highlighting (Python, JavaScript, TypeScript, C#, Java, Go, Rust, C, C++, Ruby, PHP, Swift, Kotlin, SQL, HTML, CSS, JSON, YAML, Bash, PowerShell). | P0 | Unit test: tokenize a simple snippet for each language. Assert non-empty token lists. |
| AC-4.2.5 | Long lines in code blocks do not overflow the page width. They wrap (with indentation preserved) or are truncated with a visible indicator. | P0 | Integration test: code block with a 200-character line. Assert the text is present in the DOCX and the code paragraph width does not exceed the content area. |
| AC-4.2.6 | Code block border (optional, per theme) is rendered around the block. | P1 | Integration test: theme with `code.border`. Assert paragraph border properties match. |

### US-4.3: Blockquotes

> As a user, I want blockquotes visually distinct with a colored left border.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-4.3.1 | Blockquotes render with a left border, left indent, and italic text per theme definition. | P1 | Integration test: assert paragraph has left border matching `blockquote.leftBorderColor` and `leftBorderWidth`, indentation matching `blockquote.leftIndent`, and italic run property. |
| AC-4.3.2 | Nested blockquotes increase left indentation per nesting level. | P1 | Integration test: 3-level nested blockquote. Assert increasing left indentation. |
| AC-4.3.3 | Blockquotes containing other block elements (lists, code blocks, paragraphs) render all inner elements with the blockquote indentation. | P1 | Integration test: blockquote containing a list. Assert list paragraphs are additionally indented by the blockquote amount. |

### US-4.4: Footnotes

> As a user, I want footnotes with bidirectional navigation so the reader can jump between reference and definition.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-4.4.1 | Footnote references (`[^1]`) produce superscript numbers in the body text. | P1 | Integration test: assert superscript run with the footnote number. |
| AC-4.4.2 | Footnote definitions appear at the end of the document (or bottom of the page, per Word's footnote handling) with matching numbers. | P1 | Integration test: assert OOXML `FootnoteReference` and corresponding `Footnote` part entries. |
| AC-4.4.3 | Clicking a footnote reference navigates to its definition. Clicking the definition number navigates back to the reference. | P1 | Integration test: assert bidirectional hyperlinks or Word footnote navigation structure. |

### US-4.5: Admonitions / Callouts

> As a user, I want admonitions (NOTE, WARNING, TIP, IMPORTANT, CAUTION) rendered as visually distinct callout boxes.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-4.5.1 | Each admonition type (note, warning, tip, important, caution) renders with a distinct left border or background color per theme. | P1 | Integration test: one of each type. Assert distinct `ParagraphBorder` or `Shading` colors, each matching the theme's admonition color for that type. |
| AC-4.5.2 | Admonitions display a label (e.g., "Note", "Warning") in bold before the content. | P1 | Integration test: assert first run of the admonition paragraph is bold and contains the type label. |
| AC-4.5.3 | Admonition content supports inline formatting (bold, italic, code, links). | P1 | Integration test: admonition with inline formatting. Assert formatting is preserved. |
| AC-4.5.4 | Custom admonition titles (`:::note Custom Title`) use the custom title instead of the default label. | P1 | Integration test: assert custom title appears instead of "Note". |

### US-4.6: Definition Lists

> As a user, I want definition lists rendered with clear term/definition visual separation.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-4.6.1 | Definition terms are rendered as bold paragraphs. | P1 | Integration test: assert term paragraph runs are bold. |
| AC-4.6.2 | Definitions are indented below their term. | P1 | Integration test: assert definition paragraphs have left indentation greater than the term. |
| AC-4.6.3 | Multiple definitions for a single term are all rendered, each indented. | P1 | Integration test: term with 3 definitions. Assert all 3 appear indented. |

### US-4.7: LaTeX Math Rendering

> As a user, I want LaTeX math expressions rendered as native OOXML math (not images) so they are editable, scalable, and respect document fonts.

**Rendering path: LaTeX -> KaTeX (via Playwright) -> MathML -> MML2OMML.xsl -> OMML. Requires Chromium.**

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-4.7.1 | Inline math (`$E = mc^2$`) renders as an inline OMML element within the paragraph. | P1 | Integration test: assert `OfficeMath` element within the paragraph (not a separate paragraph). |
| AC-4.7.2 | Display math (`$$\int_0^1 f(x) dx$$`) renders as a centered OMML element in its own paragraph. | P1 | Integration test: assert `OfficeMath` element in a centered paragraph. |
| AC-4.7.3 | The following LaTeX constructs produce correct OMML: fractions (`\frac`), superscripts (`^`), subscripts (`_`), Greek letters (`\alpha`, `\beta`, etc.), summation (`\sum`), integrals (`\int`), square roots (`\sqrt`), matrices (`\begin{bmatrix}`). | P1 | Unit test: each construct individually. Assert OMML output matches expected structure (compare against known-good OMML). |
| AC-4.7.4 | When Chromium is not installed and the document contains math, md2 emits a warning per math block and renders the LaTeX source as a code span/block instead. The tool does not crash. | P1 | Integration test: mock Playwright unavailability. Assert warnings on stderr and code-formatted LaTeX in the output. |
| AC-4.7.5 | Math rendering of 20 inline expressions and 5 display expressions completes in under 10 seconds (including Chromium startup). | P1 | Performance test: timed conversion of a math-heavy document. |

### US-4.8: Mermaid Diagram Rendering

> As a user, I want Mermaid diagrams rendered as high-resolution PNG images embedded in the DOCX.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-4.8.1 | Fenced code blocks with language `mermaid` are rendered to PNG at 2x DPI by default (configurable via `--mermaid-scale`). | P1 | Integration test: Mermaid flowchart code block. Assert DOCX contains an embedded PNG. Assert image dimensions are at least 2x the logical size (e.g., a 400px logical width produces an 800px image). |
| AC-4.8.2 | Rendered diagrams are embedded in the DOCX at the position of the original code block. | P1 | Integration test: Markdown with text, then Mermaid, then more text. Assert image appears between the two text sections in the DOCX. |
| AC-4.8.3 | When `--no-mermaid` is specified, Mermaid code blocks are rendered as plain code blocks (no diagram rendering). | P1 | Integration test: `--no-mermaid` flag. Assert code block text is preserved, no image. |
| AC-4.8.4 | When Chromium is not installed and the document contains Mermaid diagrams, md2 emits a warning per diagram and renders the Mermaid source as a code block. The tool does not crash. | P1 | Integration test: mock Playwright unavailability. Assert warnings and code blocks. |
| AC-4.8.5 | Diagrams are cached by content hash. Re-running md2 on the same document does not re-render unchanged Mermaid blocks. | P1 | Integration test: run twice, assert second run is faster (no Playwright invocation for cached diagrams). |
| AC-4.8.6 | `--mermaid-js <path>` flag allows the user to supply their own Mermaid JS file instead of the bundled version. | P1 | Integration test: supply a Mermaid JS file. Assert it is used (e.g., test with a different Mermaid version that renders a slightly different output). |
| AC-4.8.7 | 10 representative Mermaid diagrams (flowchart, sequence, class, state, ER, gantt, pie, mindmap, gitgraph, C4) render in under 15 seconds total including Chromium cold start. Per-diagram budget after warm: under 2 seconds. | P1 | Performance test: benchmark with 10 representative diagrams. |

---

## Feature Area 5: Style and Theme System

### US-5.1: Built-in Style Presets

> As a user, I want 3-5 visually distinct built-in presets so I can produce good-looking documents without configuration.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-5.1.1 | 5 presets are available: `default`, `technical`, `corporate`, `academic`, `minimal`. | P2 | `md2 theme list` outputs all 5 names. |
| AC-5.1.2 | Each preset produces a visually distinct DOCX when applied to the same Markdown input. "Visually distinct" means at minimum: different heading fonts or sizes, different color palette, different body font or spacing. | P2 | Visual regression test: convert reference Markdown with each preset. Snapshot comparison confirms they differ. Manual visual inspection at initial creation. |
| AC-5.1.3 | The `default` preset produces a clean, professional document without any user configuration. It is the style applied when no `--preset`, `--theme`, or `--template` is specified. | P0 | Integration test: convert with no style flags. Assert DOCX styles match the `default` preset YAML values. |
| AC-5.1.4 | Each preset is a standalone YAML file in `presets/` that can be used as a starting point for a custom theme (copy, modify, use with `--theme`). | P2 | File existence test: all 5 files exist and are valid YAML. Schema validation passes. |
| AC-5.1.5 | Modifying a preset definition changes only the output of documents using that preset. No cascading effect on other presets or the code. | P0 | This is an architecture constraint. Verified by: modifying one preset's heading font and asserting other presets' output is unchanged. |

### US-5.2: YAML Theme Files

> As a user, I want to define custom styles in a YAML file so I can match my organization's brand without modifying code.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-5.2.1 | A YAML theme file is loaded via `--theme <path>` and its values override preset defaults. | P1 | Integration test: theme YAML with `heading1.fontSize: 36pt`. Assert DOCX heading 1 font size is 36pt, not the preset default. |
| AC-5.2.2 | Theme YAML supports all style properties documented in the schema: typography (fonts, sizes, spacing), colors (primary, secondary, accent, text, code background, admonition colors), headings 1-4, body, blockquote, code, table, header, footer. | P1 | Unit test: parse a theme YAML with every documented property. Assert all values are present in the resulting `ThemeDefinition`. |
| AC-5.2.3 | Partial theme YAML files are valid. Missing properties fall through to the next cascade layer (preset, then template). | P1 | Integration test: theme YAML with only `heading1.fontSize`. Assert all other properties come from the preset. |
| AC-5.2.4 | Invalid theme YAML produces a clear error message with the property path and expected type. Example: `heading1.fontSize: "big"` produces `"Error in theme.yaml: docx.heading1.fontSize must be a measurement (e.g., '14pt', '1.5em'), got 'big'"`. | P1 | Unit test: various invalid values. Assert error messages contain the property path and guidance. |
| AC-5.2.5 | Unknown properties in theme YAML are ignored (not rejected) for forward compatibility. | P1 | Unit test: theme YAML with `futureProperty: value`. Assert no error; property is silently ignored. |
| AC-5.2.6 | `${...}` variable interpolation is NOT supported in v1. If a theme file contains `${...}` syntax, a clear warning is emitted: `"Variable interpolation (${...}) is not supported in this version. Use literal values."` | P1 | Unit test: theme YAML with `${colors.primary}`. Assert warning message. |

### US-5.3: Custom DOCX Template Cascade

> As a user, I want to supply my corporate DOCX template and have md2 use its styles while filling gaps with sensible defaults and warning me about missing styles.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-5.3.1 | `--template <path>` loads a DOCX file and extracts its style definitions as the lowest cascade layer. | P0 | Integration test: template with a custom Heading1 style (e.g., red, 30pt). Convert with `--template`. Assert DOCX heading 1 matches the template's style (since no higher layer overrides it). |
| AC-5.3.2 | When the template is missing styles that md2 needs (e.g., no Heading4, no CodeBlock style), md2 fills the gap from the preset/default and emits a warning per missing style. | P0 | Integration test: template with only Heading1-Heading3 defined. Markdown with H4. Assert warning on stderr mentioning "Heading4" and assert H4 is styled from the preset default. |
| AC-5.3.3 | Template styles do NOT override higher cascade layers. If a user specifies `--style "heading1.fontSize=24pt"`, that value wins over the template's heading1 font size. | P0 | Integration test: template with 30pt heading1 + `--style "heading1.fontSize=24pt"`. Assert DOCX heading1 is 24pt. |
| AC-5.3.4 | IRM-protected DOCX templates are detected by file header magic number and produce a clear error message with step-by-step remediation guidance. Exit code is 2. | P0 | Unit test: file with OLE compound document header. Assert error message contains "IRM" or "protected" and remediation steps. Assert exit code 2. |
| AC-5.3.5 | Legacy `.doc` files produce a specific error: `"This is a legacy Word format (.doc). Save as .docx in Word first."` | P0 | Unit test: file with `.doc` extension. Assert specific error message. |
| AC-5.3.6 | Macro-enabled files (`.docm`) produce a warning and are refused by default. | P1 | Unit test: file with `.docm` extension. Assert warning about macros and refusal. |
| AC-5.3.7 | Template files larger than the configured size limit (default 50MB, configurable via `--max-template-size`) are rejected with a clear message. | P1 | Unit test: assert rejection when file size exceeds limit. Assert the message includes the limit and suggests checking if the file is a legitimate template. |

### US-5.4: Style Cascade Resolution and Debugging

> As a user, I want to understand where each style property comes from when multiple layers are in play.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-5.4.1 | `md2 theme resolve` displays a table of all style properties, their resolved values, and which cascade layer each came from. | P1 | Integration test: run `md2 theme resolve --preset technical --style "heading1.fontSize=24pt"`. Assert output contains rows for each property with columns: Property, Value, Source. Assert `heading1.fontSize` shows "24pt" from "--style override (Layer 4)". |
| AC-5.4.2 | `md2 theme resolve` accepts the same `--preset`, `--theme`, `--template`, and `--style` flags as the convert command. | P1 | Integration test: all 4 flags together produce output without error. |
| AC-5.4.3 | `md2 theme resolve` with no flags shows the default preset resolution. | P1 | Integration test: assert output shows all properties from "preset:default (Layer 2)". |

---

## Feature Area 6: Theme Management Commands

### US-6.1: Theme Extraction

> As a user, I want to extract a theme from an existing DOCX template so I can tweak it and use it as my custom theme.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-6.1.1 | `md2 theme extract <template.docx> -o theme.yaml` reads the DOCX's style definitions and produces a valid theme YAML file. | P1 | Integration test: extract from a DOCX with custom styles. Assert output YAML parses without error and contains values matching the DOCX's style definitions. |
| AC-6.1.2 | Extracted YAML includes comments explaining inferred vs. explicit values. | P1 | Integration test: assert output contains YAML comments (lines starting with `#`). |
| AC-6.1.3 | Extracted theme round-trips: `md2 theme extract corp.docx -o corp.yaml` followed by `md2 doc.md --theme corp.yaml` produces output with styles matching the original template. | P1 | Integration test: extract then convert. Compare style properties of the output against the original template's styles. Tolerance: font names and sizes must match exactly; colors within 1 shade (due to theme color vs. explicit color mapping). |

### US-6.2: Theme Validation

> As a user, I want to validate my theme YAML before converting so I catch errors early.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-6.2.1 | `md2 theme validate <theme.yaml>` reports schema errors with line numbers and property paths. | P1 | Integration test: YAML with errors. Assert output contains line numbers and property paths for each error. |
| AC-6.2.2 | `md2 theme validate` on a valid theme file reports success and exits with code 0. | P1 | Integration test: valid YAML. Assert exit code 0 and success message. |
| AC-6.2.3 | `md2 theme validate` warns about properties that are valid but unusual (e.g., `baseFontSize: 4pt` -- valid but probably wrong). | P2 | Integration test: theme with 4pt body font. Assert warning about unusually small font size. |

### US-6.3: Theme Listing

> As a user, I want to see what built-in presets are available.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-6.3.1 | `md2 theme list` outputs the name and description of each built-in preset. | P1 | Integration test: assert output contains all 5 preset names and their `meta.description` values. |

---

## Feature Area 7: CLI UX and Diagnostics

### US-7.1: Core Conversion Command

> As a user, I want a simple, predictable CLI that converts Markdown to DOCX with minimal required arguments.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-7.1.1 | `md2 input.md -o output.docx` converts the file and produces a valid DOCX. | P0 | E2E test: convert a representative Markdown file. Assert output file exists, is a valid ZIP, and opens with Open XML SDK without error. |
| AC-7.1.2 | When `-o` is omitted, the output file name is derived from the input: `input.md` produces `input.docx` in the same directory. | P0 | E2E test: convert without `-o`. Assert file `input.docx` exists alongside `input.md`. |
| AC-7.1.3 | Exit code 0 on success. Exit code 1 on general error. Exit code 2 on protected template. | P0 | E2E test: successful conversion returns 0. Missing input file returns 1. IRM template returns 2. |
| AC-7.1.4 | Errors are written to stderr. Warnings are written to stderr. Output file path confirmation is written to stdout. | P0 | E2E test: capture stdout and stderr separately. Assert correct stream for each message type. |
| AC-7.1.5 | `-q` / `--quiet` suppresses warnings (only errors and the output path are emitted). | P0 | E2E test: convert with warnings (e.g., missing template style) and `--quiet`. Assert no warnings on stderr. |
| AC-7.1.6 | `-v` / `--verbose` shows style warnings, timing per pipeline phase, and cascade resolution details. | P1 | E2E test: convert with `--verbose`. Assert output contains timing information and style warnings. |
| AC-7.1.7 | Input file not found produces: `"Error: File not found: <path>"`. Not a stack trace. | P0 | E2E test: nonexistent input. Assert clean error message, no exception details. |
| AC-7.1.8 | `md2 --help` produces well-formatted help text listing all commands and global options. | P0 | E2E test: assert help text contains command names (convert, preview, theme, doctor) and option descriptions. |
| AC-7.1.9 | `md2 --version` outputs the version number. | P0 | E2E test: assert output matches the assembly version pattern (e.g., `md2 1.0.0`). |

### US-7.2: Pipeline Inspection

> As a user, I want to inspect intermediate pipeline stages for debugging and learning.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-7.2.1 | `--dry-run` parses and transforms the Markdown but does not produce an output file. Prints a summary: number of headings, paragraphs, images, tables, code blocks, footnotes, math expressions, Mermaid diagrams. | P1 | E2E test: `--dry-run` on representative Markdown. Assert no output file. Assert summary with correct counts. |
| AC-7.2.2 | `--stage parse --emit json` outputs the Markdig AST as JSON after parsing (before transforms). | P1 | E2E test: assert valid JSON output. Assert it contains raw AST nodes (e.g., no smart typography applied). |
| AC-7.2.3 | `--stage transform --emit json` outputs the AST after all transforms. | P1 | E2E test: assert valid JSON output. Assert it contains transformed nodes (e.g., smart quotes applied). |

### US-7.3: Diagnostic Command

> As a user, I want a command that tells me if my environment is correctly set up.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-7.3.1 | `md2 doctor` checks and reports: .NET runtime version, Playwright installation status, Chromium availability, TextMateSharp native library status. | P1 | E2E test: run `md2 doctor`. Assert output contains a status line for each check (pass/fail/warning). |
| AC-7.3.2 | Each check reports pass, fail, or warning with actionable guidance. Example: `"Chromium: Not installed. Run 'md2 doctor --install' or 'playwright install chromium' to set up Mermaid/math rendering."` | P1 | E2E test: with Chromium not installed, assert the specific guidance message. |
| AC-7.3.3 | `md2 doctor` exits with code 0 if all checks pass, code 1 if any check fails. | P1 | E2E test: assert exit code. |

### US-7.4: Multi-File Concatenation

> As a user, I want to combine multiple Markdown files into a single DOCX.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-7.4.1 | `md2 file1.md file2.md file3.md -o combined.docx` concatenates the files in order and produces a single DOCX. | P2 | E2E test: three Markdown files, each with a heading. Assert output DOCX contains all three headings in order. |
| AC-7.4.2 | Each input file's front matter is merged. First file's metadata takes priority for document-level properties (title, author). | P2 | E2E test: two files with different `title` in front matter. Assert document title matches the first file's title. |
| AC-7.4.3 | Relative image paths in each file resolve relative to that file's directory, not the working directory. | P2 | E2E test: two files in different directories, each referencing a local image. Assert both images are embedded. |
| AC-7.4.4 | Files are separated by a page break by default. `--no-file-break` concatenates without page breaks. | P2 | E2E test: assert section break between files by default. Assert no section break with `--no-file-break`. |

---

## Feature Area 8: Preview

### US-8.1: Hot-Reload Preview

> As a user, I want to preview my document in a browser while editing, with instant reload on save.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-8.1.1 | `md2 preview input.md` opens a browser window showing the rendered document. | P1 | Manual test (requires display): command opens a browser. Automated: assert `PreviewServer` starts and HTTP endpoint responds with HTML. |
| AC-8.1.2 | Saving the Markdown file triggers a re-render and the browser updates without manual refresh. Latency from save to visual update is under 500ms for a typical document (< 100 KB Markdown). | P1 | Integration test: modify the watched file, assert the server sends a reload signal within 500ms. |
| AC-8.1.3 | Preview uses the same theme/preset as the final DOCX output (or a close HTML approximation). | P1 | Manual test: visual comparison of preview and DOCX output. Automated: assert HTML contains CSS properties derived from the resolved theme (font families, colors). |
| AC-8.1.4 | `Ctrl+C` in the terminal cleanly stops the preview server and closes the browser. | P1 | Integration test: send SIGINT, assert server port is released and Playwright browser is closed. |
| AC-8.1.5 | Preview works without `--template` (uses preset/theme only). Template-specific styles are best-effort in preview. | P1 | Integration test: preview with `--preset corporate` and no template. Assert no error. |

---

## Feature Area 9: Document Metadata

### US-9.1: DOCX Document Properties

> As a user, I want front matter metadata mapped to DOCX document properties so the file is properly cataloged.

| ID | Criterion | Priority | Verification |
|----|-----------|----------|--------------|
| AC-9.1.1 | `title` from front matter is set as the DOCX `Title` core property. | P0 | Integration test: assert `PackageProperties.Title` matches front matter `title`. |
| AC-9.1.2 | `author` from front matter is set as the DOCX `Creator` core property. | P0 | Integration test: assert `PackageProperties.Creator` matches front matter `author`. |
| AC-9.1.3 | `date` from front matter is set as the DOCX `Created` core property. | P0 | Integration test: assert `PackageProperties.Created` matches front matter `date`. |
| AC-9.1.4 | `subject` and `keywords` from front matter are set as the corresponding DOCX core properties. | P1 | Integration test: assert corresponding `PackageProperties` fields. |
| AC-9.1.5 | When no front matter is present, document properties are not set (they remain empty, not null or garbage). | P0 | Integration test: Markdown without front matter. Assert `PackageProperties.Title` is null or empty. |

---

## Smallest Shippable Increment (SSI)

Even though the human prefers complete delivery, if we needed to ship the smallest useful thing:

**SSI = Wave 1 + default preset + core CLI**

This gives: Markdown parsing (CommonMark + GFM), DOCX output with headings, body text, inline formatting, lists, images, links, page numbers, configurable page size/margins, front matter to document properties, and the default style preset. No syntax highlighting, no math, no Mermaid, no TOC, no cover page, no template cascade.

**Why this is the floor:** It already beats pandoc on typography quality (widow/orphan control, proper spacing, readable defaults) and style correctness (built-in preset vs. pandoc's bare styles). It is a daily-driver for simple documents.

**Why we are not shipping this:** The human's scope appetite is "complete." The tiered model calls out tables, code blocks, and syntax highlighting as P0. Math, Mermaid, TOC, and admonitions are P1 differentiators. The whole point is to be noticeably better than pandoc across the board, not just for simple docs.

---

## Acceptance Summary

| Priority | Count | Description |
|----------|-------|-------------|
| **P0** | 48 | Ship-blocking. Must pass before v1 is declared done. |
| **P1** | 56 | Expected differentiators. Omission would make the tool feel incomplete. |
| **P2** | 8 | Delighters. Nice to have, not ship-blocking, but planned for v1. |
| **Total** | 112 | |

### Done Gate for v1

v1 is done when:
1. All P0 criteria pass their verification tests.
2. All P1 criteria pass OR have a documented, accepted deferral with rationale.
3. All P2 criteria pass OR are explicitly deferred to a point release.
4. The 5-day table auto-sizing prototype gate has been cleared.
5. The Mermaid performance benchmark (10 diagrams < 15s) has been met.
6. The math performance benchmark (25 expressions < 10s) has been met.
7. All 5 presets have been visually reviewed and approved by the human.
8. `md2 doctor` reports all-green on both Windows and Linux.
9. End-to-end test with a representative 20-page Markdown document (headings, tables, code, images, math, Mermaid, footnotes, admonitions) produces a DOCX that opens correctly in Microsoft Word and LibreOffice Writer.
