// agent-notes: { ctx: "20-page comprehensive e2e validation — all element types", deps: [Md2.Core, Md2.Parsing, Md2.Emit.Docx, Md2.Highlight, DocumentFormat.OpenXml], state: active, last: "tara@2026-03-13" }

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Ast;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Emit.Docx;
using Md2.Highlight;
using Md2.Parsing;
using Shouldly;

namespace Md2.Integration.Tests;

/// <summary>
/// Comprehensive 20-page document validation (Issue #57).
/// Exercises every supported Markdown element type through the full pipeline
/// and validates the DOCX output structure. Serves as a release confidence gate.
/// </summary>
public class ComprehensiveDocumentTests : IAsyncLifetime
{
    private WordprocessingDocument _wordDoc = null!;
    private MemoryStream _stream = null!;
    private Body _body = null!;

    /// <summary>
    /// Representative Markdown covering all supported element types.
    /// Generates roughly 20 pages of DOCX output.
    /// </summary>
    private const string ComprehensiveMarkdown = """"
        ---
        title: Comprehensive Document Validation
        author: md2 Test Suite
        date: 2026-03-13
        subject: E2E Validation
        keywords: test, comprehensive, validation
        ---

        # Chapter 1: Introduction

        This document exercises **every supported Markdown element type** through the md2 pipeline.
        It serves as a *release confidence gate* for the md2 v1 converter. Every element below
        must survive the parse → transform → emit pipeline and produce valid Open XML.

        The document includes ~~deprecated approaches~~ that have been superseded by modern
        alternatives. For inline code, see `Console.WriteLine("Hello")` as an example.

        Visit the [md2 repository](https://github.com/example/md2doc) for more information,
        or check the [architecture docs](https://example.com/arch).

        ---

        ## Chapter 2: Text Formatting

        ### 2.1 Inline Styles

        Regular text with **bold**, *italic*, ***bold italic***, ~~strikethrough~~, and `inline code`.

        A paragraph with mixed formatting: The **quick** *brown* fox ~~jumps~~ `over` the lazy dog.

        ### 2.2 Multiple Paragraphs

        First paragraph with some body text that extends across multiple lines to ensure proper
        paragraph handling in the output document. This tests word wrapping and paragraph spacing.

        Second paragraph continues the narrative. Good typography matters — notice the em dash,
        the "smart quotes," and the ellipsis...

        Third paragraph. We need sufficient content to generate multiple pages. The DOCX emitter
        must handle long documents gracefully without running out of memory or producing corrupt
        output. This paragraph adds bulk to reach our 20-page target.

        ### 2.3 Line Breaks

        This line has a hard break
        that continues here.

        ## Chapter 3: Headings

        ### 3.1 Third-level heading

        Content under H3.

        #### 3.1.1 Fourth-level heading

        Content under H4.

        ##### Fifth-level heading

        Content under H5.

        ###### Sixth-level heading

        Content under H6.

        ## Chapter 4: Lists

        ### 4.1 Unordered Lists

        - First item
        - Second item with **bold**
        - Third item with `code`
          - Nested item one
          - Nested item two
            - Deeply nested item
        - Fourth item

        ### 4.2 Ordered Lists

        1. First numbered item
        2. Second numbered item
        3. Third numbered item with *emphasis*
           1. Sub-item one
           2. Sub-item two
        4. Fourth numbered item

        ### 4.3 Task Lists

        - [x] Completed task
        - [x] Another completed task
        - [ ] Pending task
        - [ ] Another pending task with **bold text**

        ### 4.4 Mixed List Content

        1. A list item with a [link](https://example.com)
        2. A list item with `inline code` and **bold** text
        3. A plain list item

        ## Chapter 5: Tables

        ### 5.1 Simple Table

        | Feature | Status | Priority |
        |---------|--------|----------|
        | Parsing | Done | High |
        | Themes | Done | High |
        | Preview | Planned | Medium |

        ### 5.2 Table with Formatting

        | Element | Syntax | Example |
        |---------|--------|---------|
        | Bold | `**text**` | **bold text** |
        | Italic | `*text*` | *italic text* |
        | Code | `` `code` `` | `code text` |
        | Link | `[text](url)` | [link text](https://example.com) |
        | Strike | `~~text~~` | ~~struck text~~ |

        ### 5.3 Wide Table

        | ID | Component | Description | Owner | Status | Last Updated | Notes |
        |----|-----------|-------------|-------|--------|-------------|-------|
        | 1 | Parser | Markdig pipeline with extensions | Team A | Active | 2026-03-01 | GFM compatible |
        | 2 | Transforms | AST transform chain | Team A | Active | 2026-03-05 | 7 transforms |
        | 3 | Theme Engine | YAML DSL with cascade resolver | Team B | Active | 2026-03-08 | 4-layer merge |
        | 4 | DOCX Emitter | Open XML SDK output | Team A | Active | 2026-03-10 | 8 builders |
        | 5 | CLI | System.CommandLine interface | Team B | Active | 2026-03-12 | 5 subcommands |

        ## Chapter 6: Code Blocks

        ### 6.1 C# Code

        ```csharp
        using System;

        namespace Example;

        public class Calculator
        {
            public int Add(int a, int b) => a + b;

            public double Divide(double numerator, double denominator)
            {
                if (denominator == 0)
                    throw new DivideByZeroException("Cannot divide by zero");

                return numerator / denominator;
            }
        }
        ```

        ### 6.2 Python Code

        ```python
        def fibonacci(n: int) -> list[int]:
            """Generate Fibonacci sequence up to n terms."""
            if n <= 0:
                return []
            elif n == 1:
                return [0]

            fib = [0, 1]
            for _ in range(2, n):
                fib.append(fib[-1] + fib[-2])
            return fib

        # Generate first 10 Fibonacci numbers
        result = fibonacci(10)
        print(f"Fibonacci: {result}")
        ```

        ### 6.3 JSON Data

        ```json
        {
          "name": "md2",
          "version": "1.0.0",
          "description": "Markdown to DOCX converter",
          "dependencies": {
            "Markdig": "0.38.0",
            "DocumentFormat.OpenXml": "3.2.0",
            "YamlDotNet": "16.3.0"
          }
        }
        ```

        ### 6.4 Plain Code Block

        ```
        This is a plain code block without language specification.
        It should still use monospace font and background shading.
        Line 1
        Line 2
        Line 3
        ```

        ## Chapter 7: Blockquotes

        ### 7.1 Simple Blockquote

        > This is a blockquote. It should have a colored left border and indentation.
        > It can span multiple lines.

        ### 7.2 Nested Blockquotes

        > Outer blockquote
        >
        > > Inner blockquote with **bold** text
        > >
        > > > Deeply nested blockquote
        >
        > Back to outer level

        ### 7.3 Blockquote with Formatting

        > **Important:** This blockquote contains *formatted* text, `inline code`,
        > and a [link](https://example.com). All formatting should be preserved.

        ## Chapter 8: Footnotes

        This paragraph references a footnote[^1] about the architecture.
        Another reference[^2] discusses the theme engine.
        A third footnote[^longnote] has a longer identifier.

        [^1]: The architecture follows a pipeline pattern with discrete transform stages.
        [^2]: The theme engine supports a 4-layer cascade: CLI > YAML > preset > template.
        [^longnote]: This footnote has a longer identifier to test non-numeric footnote IDs.

        ## Chapter 9: Admonitions

        ### 9.1 Note

        !!! note "Important Note"
            This is a note admonition. It should have a distinctive visual style
            with an appropriate icon or label.

        ### 9.2 Warning

        !!! warning "Caution Required"
            This is a warning admonition. Users should pay attention to this content.
            It contains **bold** and *italic* formatting.

        ### 9.3 Tip

        !!! tip
            A tip without a custom title. The default title should be "Tip".

        ### 9.4 Info

        !!! info "Additional Information"
            An informational admonition with a `code snippet` inside.

        ## Chapter 10: Definition Lists

        Term 1
        :   Definition for term 1. This is a simple definition.

        Term 2
        :   Definition for term 2. This definition is longer and contains
            more text to test multi-line definition rendering.

        Complex Term
        :   First definition for the complex term.
        :   Second definition for the same term.

        ## Chapter 11: Horizontal Rules

        Content above the rule.

        ---

        Content between rules.

        ***

        Content below the rules.

        ## Chapter 12: Extended Content for Page Count

        ### 12.1 Technical Architecture Overview

        The md2 converter follows a pipeline architecture where Markdown source flows through
        discrete processing stages. The parser uses Markdig with CommonMark and GFM extensions
        to produce an abstract syntax tree. This AST then passes through an ordered chain of
        transforms that annotate nodes with rendering data — syntax highlighting tokens, OMML
        math expressions, Mermaid diagram PNGs, and typography corrections.

        The theme engine resolves styles through a four-layer cascade. At the base, a built-in
        template provides sensible defaults. The next layer applies a named preset (default,
        technical, corporate, academic, or minimal). User-provided YAML theme files override
        preset values, and finally CLI flags take highest priority. This cascade produces a
        ResolvedTheme — a fully-specified style object with no null values.

        The DOCX emitter walks the transformed AST using a visitor pattern. Each node type maps
        to a specialized builder: ParagraphBuilder for headings and body text, TableBuilder for
        GFM tables with auto-sizing column widths, ListBuilder for ordered and unordered lists,
        CodeBlockBuilder for syntax-highlighted code, ImageBuilder for inline and block images,
        MathBuilder for OMML equations, AdmonitionBuilder for callout boxes, and FootnoteBuilder
        for footnote references and definitions.

        ### 12.2 Quality Assurance

        Output quality is the product's reason to exist. If the output isn't noticeably better
        than pandoc, the tool has no purpose. Every design decision is evaluated against this bar.
        Typography matters — smart quotes, proper dashes, controlled line spacing. Layout matters —
        widow and orphan control, page-aware table splitting, balanced column widths. Color matters —
        the five built-in presets provide distinct visual identities while maintaining readability.

        ### 12.3 Implementation Details

        The CLI uses System.CommandLine for argument parsing, which provides automatic help text,
        tab completion, and response file support. The root command is `md2` (the convert command),
        with subcommands for theme management (`md2 theme resolve`, `md2 theme list`, etc.) and
        diagnostics (`md2 doctor`).

        Error handling follows a layered approach. All md2-specific exceptions extend Md2Exception,
        which carries a user-friendly UserMessage alongside the developer-oriented Message. The CLI
        catches Md2Exception and displays UserMessage to stderr. Unexpected exceptions trigger a
        generic error with a suggestion to run `--debug` for diagnostics.

        ### 12.4 Performance Considerations

        The Playwright-based renderers (Mermaid diagrams and KaTeX math) are the performance
        bottleneck. Browser launch takes 2-5 seconds, and each rendering operation requires a
        page navigation and screenshot. To mitigate this, md2 uses a content-hash cache for
        diagrams — identical Mermaid source produces a cache hit, skipping the browser entirely.
        The browser instance is shared across all renderings within a single md2 invocation.

        ### 12.5 Deployment Model

        md2 is distributed as a .NET global tool. Users install it with `dotnet tool install -g md2`
        and run it from any directory. The tool is self-contained except for the Chromium browser
        required by Playwright. On first run, `md2 doctor` checks for Chromium and provides
        installation guidance if it's missing.

        ## Chapter 13: Additional Tables for Bulk

        ### 13.1 Feature Matrix

        | Feature | Default | Technical | Corporate | Academic | Minimal |
        |---------|---------|-----------|-----------|----------|---------|
        | Serif body font | Yes | No | No | Yes | No |
        | Color accents | Blue | Teal | Navy | Burgundy | Gray |
        | Code background | Light gray | Dark | Light gray | Cream | White |
        | Table borders | Full | Full | Full | Full | Minimal |
        | Heading weight | Bold | Bold | Bold | Small caps | Light |
        | Line spacing | 1.15 | 1.15 | 1.25 | 1.5 | 1.15 |

        ### 13.2 Sprint History

        | Sprint | Items | Completed | Velocity | Notes |
        |--------|-------|-----------|----------|-------|
        | 1 | 6 | 6 | 6 | Foundation: parser, pipeline, basic emitter |
        | 2 | 8 | 8 | 8 | Tables, lists, images, code blocks |
        | 3 | 7 | 7 | 7 | Syntax highlighting, footnotes, admonitions |
        | 4 | 5 | 5 | 5 | Mermaid diagrams, math rendering |
        | 5 | 8 | 8 | 8 | Theme engine, presets, cascade resolver |
        | 6 | 7 | 7 | 7 | CLI commands, template safety, validation |
        | 7 | 6 | 6 | 6 | TOC, cover page, cross-references, headers |
        | 8 | 3 | 3 | 3 | Metadata, cancellation, CLI polish |

        ## Chapter 14: More Prose for Page Count

        The history of document conversion tools stretches back to the early days of computing,
        when the distinction between presentation and content first became a practical concern.
        Early systems like troff and TeX established the principle that document source should
        describe structure, not appearance — a principle that Markdown carries forward today.

        Markdown's popularity stems from its simplicity. A plain text file with a handful of
        punctuation conventions produces readable source and structured output. But simplicity
        comes at a cost: Markdown's original specification is deliberately minimal, leaving many
        common document elements undefined. Extensions like GFM (GitHub Flavored Markdown) add
        tables, task lists, and strikethrough, while other extensions provide math, admonitions,
        and definition lists.

        Converting Markdown to a polished Word document requires bridging two very different
        paradigms. Markdown is a stream of block and inline elements with minimal styling hints.
        DOCX is a complex XML format with explicit style definitions, paragraph properties, run
        formatting, relationship parts, and content types. The converter must map between these
        worlds while preserving the author's intent and producing output that looks professional.

        This is where md2 distinguishes itself from existing tools. Pandoc is an excellent
        universal converter, but its DOCX output uses minimal styling and requires post-processing
        to match professional standards. md2 focuses exclusively on the Markdown-to-DOCX path,
        allowing it to optimize every aspect of the output: typography, color, spacing, table
        layout, code highlighting, and page structure.

        The theme engine is central to this quality advantage. Rather than hardcoding styles,
        md2 uses a YAML-based theme DSL that separates style concerns from conversion logic.
        Users can create custom themes or choose from five built-in presets, each designed to
        serve a specific use case. The cascade resolver ensures that customization is additive —
        you only need to specify what you want to change, and everything else inherits from the
        preset defaults.

        Looking forward, the v2 roadmap includes PPTX output, multi-file concatenation, and
        pipeline inspection tools. The architecture was designed with these extensions in mind:
        the IFormatEmitter interface allows new output formats without changing the pipeline,
        and the transform chain can be extended with new AST visitors without modifying existing
        transforms.

        ## Chapter 15: Final Section

        ### 15.1 Summary

        This document has exercised all supported Markdown element types:

        1. **Headings** — H1 through H6
        2. **Inline formatting** — bold, italic, bold italic, strikethrough, inline code
        3. **Links** — inline hyperlinks
        4. **Lists** — ordered, unordered, nested, task lists
        5. **Tables** — simple, formatted, wide
        6. **Code blocks** — C#, Python, JSON, plain
        7. **Blockquotes** — simple, nested, formatted
        8. **Footnotes** — numeric and named
        9. **Admonitions** — note, warning, tip, info
        10. **Definition lists** — single and multiple definitions
        11. **Horizontal rules** — thematic breaks
        12. **Front matter** — title, author, date, subject, keywords

        ### 15.2 Conclusion

        If this document renders correctly as a DOCX file that opens without errors in Microsoft
        Word and LibreOffice Writer, then the md2 converter has demonstrated comprehensive element
        coverage. The output should be approximately 20 pages with consistent styling, proper page
        layout, and all formatting intact.

        > "The test of a first-rate intelligence is the ability to hold two opposed ideas in mind
        > at the same time and still retain the ability to function." — F. Scott Fitzgerald

        **End of document.**
        """";

    public async Task InitializeAsync()
    {
        var pipeline = new ConversionPipeline();
        var parserOptions = new ParserOptions();
        var doc = pipeline.Parse(ComprehensiveMarkdown, parserOptions);

        pipeline.RegisterTransform(new YamlFrontMatterExtractor());
        pipeline.RegisterTransform(new SmartTypographyTransform());
        pipeline.RegisterTransform(new SyntaxHighlightAnnotator());

        var transformOptions = new TransformOptions { SmartTypography = true };
        var transformResult = pipeline.Transform(doc, transformOptions);

        var theme = ResolvedTheme.CreateDefault();
        var emitOptions = new EmitOptions
        {
            IncludeToc = true,
            TocDepth = 3,
            IncludeCoverPage = true
        };
        var emitter = new DocxEmitter();
        _stream = new MemoryStream();

        await pipeline.Emit(transformResult.Document, theme, emitter, emitOptions, _stream);
        _stream.Position = 0;

        _wordDoc = WordprocessingDocument.Open(_stream, false);
        _body = _wordDoc.MainDocumentPart!.Document.Body!;
    }

    public Task DisposeAsync()
    {
        _wordDoc?.Dispose();
        _stream?.Dispose();
        return Task.CompletedTask;
    }

    // ── Document Structure ─────────────────────────────────────────

    [Fact]
    public void Document_IsValid()
    {
        _wordDoc.ShouldNotBeNull();
        _wordDoc.MainDocumentPart.ShouldNotBeNull();
        _wordDoc.MainDocumentPart!.Document.ShouldNotBeNull();
        _body.ShouldNotBeNull();
    }

    [Fact]
    public void Document_HasSubstantialContent()
    {
        var paragraphs = _body.Elements<Paragraph>().ToList();
        // A 20-page document should have well over 50 paragraphs
        paragraphs.Count.ShouldBeGreaterThan(50,
            $"Expected substantial content (>50 paragraphs), got {paragraphs.Count}");
    }

    [Fact]
    public void Document_StreamIsNonTrivialSize()
    {
        // A comprehensive DOCX with syntax highlighting should be at least 15KB
        _stream.Length.ShouldBeGreaterThan(15_000,
            $"DOCX file is suspiciously small ({_stream.Length} bytes) for a comprehensive document");
    }

    // ── Document Properties ────────────────────────────────────────

    [Fact]
    public void Document_HasCoreProperties()
    {
        _wordDoc.CoreFilePropertiesPart.ShouldNotBeNull();
    }

    [Fact]
    public void Document_HasStyleDefinitionsPart()
    {
        _wordDoc.MainDocumentPart!.StyleDefinitionsPart.ShouldNotBeNull();
    }

    // ── Headings ───────────────────────────────────────────────────

    [Fact]
    public void Document_ContainsAllHeadingLevels()
    {
        var paragraphs = _body.Elements<Paragraph>().ToList();
        var styleIds = paragraphs
            .Select(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value)
            .Where(s => s != null)
            .ToHashSet();

        styleIds.ShouldContain("Heading1");
        styleIds.ShouldContain("Heading2");
        styleIds.ShouldContain("Heading3");
        styleIds.ShouldContain("Heading4");
        styleIds.ShouldContain("Heading5");
        styleIds.ShouldContain("Heading6");
    }

    [Fact]
    public void Document_HeadingsHaveCorrectText()
    {
        var allText = GetAllText();

        allText.ShouldContain("Chapter 1: Introduction");
        allText.ShouldContain("Chapter 5: Tables");
        allText.ShouldContain("Chapter 8: Footnotes");
        allText.ShouldContain("Chapter 15: Final Section");
    }

    // ── Inline Formatting ──────────────────────────────────────────

    [Fact]
    public void Document_HasBoldFormatting()
    {
        var runs = _body.Descendants<Run>().ToList();
        runs.Any(r => r.RunProperties?.Bold != null).ShouldBeTrue(
            "Document should contain bold runs");
    }

    [Fact]
    public void Document_HasItalicFormatting()
    {
        var runs = _body.Descendants<Run>().ToList();
        runs.Any(r => r.RunProperties?.Italic != null).ShouldBeTrue(
            "Document should contain italic runs");
    }

    [Fact]
    public void Document_HasStrikethroughFormatting()
    {
        var runs = _body.Descendants<Run>().ToList();
        runs.Any(r => r.RunProperties?.Strike != null).ShouldBeTrue(
            "Document should contain strikethrough runs");
    }

    [Fact]
    public void Document_HasInlineCodeFormatting()
    {
        var runs = _body.Descendants<Run>().ToList();
        runs.Any(r => r.RunProperties?.Shading != null).ShouldBeTrue(
            "Document should contain inline code runs with shading");
    }

    // ── Hyperlinks ─────────────────────────────────────────────────

    [Fact]
    public void Document_ContainsHyperlinks()
    {
        var hyperlinks = _body.Descendants<Hyperlink>().ToList();
        hyperlinks.Count.ShouldBeGreaterThanOrEqualTo(3,
            "Document should contain multiple hyperlinks");
    }

    [Fact]
    public void Document_HyperlinksHaveRelationships()
    {
        var hyperlinks = _body.Descendants<Hyperlink>().ToList();
        var mainPart = _wordDoc.MainDocumentPart!;

        foreach (var hyperlink in hyperlinks.Where(h => h.Id?.Value != null))
        {
            var rel = mainPart.HyperlinkRelationships
                .FirstOrDefault(r => r.Id == hyperlink.Id!.Value);
            rel.ShouldNotBeNull(
                $"Hyperlink with Id '{hyperlink.Id!.Value}' should have a relationship");
        }
    }

    // ── Tables ─────────────────────────────────────────────────────

    [Fact]
    public void Document_ContainsTables()
    {
        var tables = _body.Elements<Table>().ToList();
        tables.Count.ShouldBeGreaterThanOrEqualTo(4,
            "Document should contain at least 4 tables");
    }

    [Fact]
    public void Document_TablesHaveGridColumns()
    {
        var tables = _body.Elements<Table>().ToList();
        var tablesWithGrid = tables.Where(t =>
            t.GetFirstChild<TableGrid>() != null).ToList();

        tablesWithGrid.Count.ShouldBeGreaterThanOrEqualTo(4,
            "At least 4 tables should have a TableGrid with column definitions");

        foreach (var table in tablesWithGrid)
        {
            table.GetFirstChild<TableGrid>()!.Elements<GridColumn>().Any().ShouldBeTrue(
                "TableGrid should contain GridColumn definitions");
        }
    }

    [Fact]
    public void Document_TablesHaveHeaderStyling()
    {
        var tables = _body.Elements<Table>().ToList();
        // At least one table should have header cell shading
        var hasHeaderShading = tables.Any(t =>
        {
            var firstRow = t.Elements<TableRow>().FirstOrDefault();
            return firstRow?.Descendants<Shading>().Any() == true;
        });
        hasHeaderShading.ShouldBeTrue(
            "At least one table should have header row shading");
    }

    [Fact]
    public void Document_WideTableHasSevenColumns()
    {
        var tables = _body.Elements<Table>().ToList();
        var wideTable = tables.FirstOrDefault(t =>
            t.GetFirstChild<TableGrid>()?.Elements<GridColumn>().Count() == 7);
        wideTable.ShouldNotBeNull(
            "Document should contain a 7-column wide table");
    }

    // ── Code Blocks ────────────────────────────────────────────────

    [Fact]
    public void Document_ContainsCodeBlockContent()
    {
        var allText = GetAllText();

        allText.ShouldContain("Calculator");
        allText.ShouldContain("fibonacci");
        allText.ShouldContain("md2");
    }

    [Fact]
    public void Document_CodeBlocksHaveSyntaxHighlighting()
    {
        // Syntax-highlighted code blocks should have runs with color
        var runs = _body.Descendants<Run>().ToList();
        var coloredRuns = runs.Where(r =>
            r.RunProperties?.Color != null &&
            r.RunProperties.Color.Val?.Value != null &&
            r.RunProperties.Color.Val.Value != "000000").ToList();

        coloredRuns.Count.ShouldBeGreaterThan(5,
            "Code blocks should have syntax-highlighted runs with colors");
    }

    // ── Lists ──────────────────────────────────────────────────────

    [Fact]
    public void Document_ContainsListContent()
    {
        var allText = GetAllText();

        // Unordered list items
        allText.ShouldContain("First item");
        allText.ShouldContain("Nested item one");
        allText.ShouldContain("Deeply nested item");

        // Ordered list items
        allText.ShouldContain("First numbered item");

        // Task list items
        allText.ShouldContain("Completed task");
        allText.ShouldContain("Pending task");
    }

    [Fact]
    public void Document_ListParagraphsHaveNumberingProperties()
    {
        var paragraphs = _body.Elements<Paragraph>().ToList();
        var numberedParagraphs = paragraphs.Where(p =>
            p.ParagraphProperties?.NumberingProperties != null).ToList();

        numberedParagraphs.Count.ShouldBeGreaterThan(10,
            "Document should have many list paragraphs with numbering properties");
    }

    // ── Blockquotes ────────────────────────────────────────────────

    [Fact]
    public void Document_ContainsBlockquoteContent()
    {
        var allText = GetAllText();

        allText.ShouldContain("This is a blockquote");
        allText.ShouldContain("Outer blockquote");
        allText.ShouldContain("Inner blockquote");
    }

    [Fact]
    public void Document_BlockquotesHaveIndentation()
    {
        var paragraphs = _body.Elements<Paragraph>().ToList();
        var indentedParagraphs = paragraphs.Where(p =>
            p.ParagraphProperties?.Indentation?.Left?.Value != null &&
            int.Parse(p.ParagraphProperties.Indentation.Left.Value, System.Globalization.CultureInfo.InvariantCulture) > 0).ToList();

        indentedParagraphs.Count.ShouldBeGreaterThan(3,
            "Blockquotes should produce indented paragraphs");
    }

    // ── Footnotes ──────────────────────────────────────────────────

    [Fact]
    public void Document_HasFootnoteBookmarks()
    {
        // md2 implements footnotes as bookmark-based bidirectional navigation,
        // not OOXML-native FootnoteReference/FootnotesPart
        var bookmarkStarts = _body.Descendants<BookmarkStart>().ToList();
        var footnoteBookmarks = bookmarkStarts
            .Where(b => b.Name?.Value?.StartsWith("footnote_") == true).ToList();

        footnoteBookmarks.Count.ShouldBeGreaterThanOrEqualTo(3,
            "Document should contain at least 3 footnote bookmarks");
    }

    [Fact]
    public void Document_FootnoteContentIsPresent()
    {
        var allText = GetAllText();
        allText.ShouldContain("pipeline pattern");
        allText.ShouldContain("4-layer cascade");
    }

    // ── Admonitions ────────────────────────────────────────────────

    [Fact]
    public void Document_ContainsAdmonitionContent()
    {
        var allText = GetAllText();

        allText.ShouldContain("Important Note");
        allText.ShouldContain("Caution Required");
    }

    // ── Definition Lists ───────────────────────────────────────────

    [Fact]
    public void Document_ContainsDefinitionListContent()
    {
        var allText = GetAllText();

        allText.ShouldContain("Term 1");
        allText.ShouldContain("Definition for term 1");
        allText.ShouldContain("Complex Term");
    }

    // ── Horizontal Rules ───────────────────────────────────────────

    [Fact]
    public void Document_ContainsThematicBreaks()
    {
        var paragraphs = _body.Elements<Paragraph>().ToList();
        var breaks = paragraphs.Where(p =>
            p.ParagraphProperties?.ParagraphBorders?.BottomBorder != null).ToList();

        breaks.Count.ShouldBeGreaterThanOrEqualTo(2,
            "Document should contain at least 2 thematic breaks (horizontal rules)");
    }

    // ── Cover Page ─────────────────────────────────────────────────

    [Fact]
    public void Document_HasCoverPage()
    {
        var allText = GetAllText();

        // Cover page should include front matter fields
        allText.ShouldContain("Comprehensive Document Validation");
        allText.ShouldContain("md2 Test Suite");
    }

    // ── Table of Contents ──────────────────────────────────────────

    [Fact]
    public void Document_HasTableOfContents()
    {
        // TOC is rendered as an SdtBlock (Structured Document Tag)
        var sdtBlocks = _body.Descendants<SdtBlock>().ToList();
        var hasToc = sdtBlocks.Any(sdt =>
        {
            var alias = sdt.SdtProperties?.GetFirstChild<SdtAlias>();
            return alias?.Val?.Value == "TOC";
        });

        if (!hasToc)
        {
            // Fallback: check for TOC field codes
            var fieldCodes = _body.Descendants<FieldCode>().ToList();
            hasToc = fieldCodes.Any(fc => fc.Text.Contains("TOC"));
        }

        hasToc.ShouldBeTrue("Document should contain a Table of Contents");
    }

    // ── Page Layout ────────────────────────────────────────────────

    [Fact]
    public void Document_HasCorrectPageLayout()
    {
        var sectionProps = _body.Elements<SectionProperties>().FirstOrDefault();
        sectionProps.ShouldNotBeNull();

        var pageSize = sectionProps!.GetFirstChild<PageSize>();
        pageSize.ShouldNotBeNull();
    }

    [Fact]
    public void Document_HasPageNumbers()
    {
        var mainPart = _wordDoc.MainDocumentPart!;
        var footerParts = mainPart.FooterParts.ToList();
        footerParts.ShouldNotBeEmpty("Document should have footer parts");

        var hasPageField = footerParts.Any(fp =>
            fp.Footer?.Descendants<FieldCode>()
                .Any(fc => fc.Text.Contains("PAGE")) == true);
        hasPageField.ShouldBeTrue("Footer should contain PAGE field for page numbers");
    }

    // ── Widow/Orphan Control ───────────────────────────────────────

    [Fact]
    public void Document_ContentParagraphsHaveWidowControl()
    {
        // Exclude known-exempt paragraphs: TOC entries, cover page elements
        var exemptStyles = new HashSet<string> { "TOCHeading", "TOC1", "TOC2", "TOC3", "CoverTitle", "CoverSubtitle", "CoverDate" };

        var contentParagraphs = _body.Elements<Paragraph>()
            .Where(p =>
            {
                var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                return styleId == null || !exemptStyles.Contains(styleId);
            })
            .ToList();

        var withWidowControl = contentParagraphs.Count(p =>
            p.ParagraphProperties?.WidowControl != null);
        var total = contentParagraphs.Count;

        // Most content paragraphs should have widow control. Some builders
        // (list items, admonition content, footnote definitions) may not set it.
        var ratio = total > 0 ? (double)withWidowControl / total : 1.0;
        ratio.ShouldBeGreaterThan(0.65,
            $"Only {withWidowControl}/{total} content paragraphs have widow control ({ratio:P0})");
    }

    // ── Smart Typography ───────────────────────────────────────────

    [Fact]
    public void Document_HasSmartTypography()
    {
        var allText = GetAllText();

        // Em dash should be present (from " — " in source)
        allText.ShouldContain("\u2014");
    }

    // ── Styles ─────────────────────────────────────────────────────

    [Fact]
    public void Document_HasStyleDefinitions()
    {
        var stylesPart = _wordDoc.MainDocumentPart!.StyleDefinitionsPart;
        stylesPart.ShouldNotBeNull("Document should have style definitions");

        var styles = stylesPart!.Styles!.Elements<Style>().ToList();
        styles.Count.ShouldBeGreaterThan(5,
            "Document should define multiple styles");
    }

    // ── DOCX File Integrity ────────────────────────────────────────

    [Fact]
    public void Document_CanBeReopened()
    {
        // Verify the document can be re-opened from the stream
        _stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(_stream, false);
        reopened.ShouldNotBeNull();
        reopened.MainDocumentPart.ShouldNotBeNull();
        reopened.MainDocumentPart!.Document.Body.ShouldNotBeNull();
    }

    [Fact]
    public void Document_HasNumberingDefinitions()
    {
        var numberingPart = _wordDoc.MainDocumentPart!.NumberingDefinitionsPart;
        numberingPart.ShouldNotBeNull(
            "Document with lists should have a NumberingDefinitionsPart");
    }

    // ── Write DOCX to Disk for Manual Validation ───────────────────

    [Fact]
    [Trait("Category", "Manual")]
    public void Document_WriteToTempForManualInspection()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "md2-comprehensive-test.docx");
        try
        {
            _stream.Position = 0;
            using var fileStream = File.Create(outputPath);
            _stream.CopyTo(fileStream);

            File.Exists(outputPath).ShouldBeTrue();
            new FileInfo(outputPath).Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────

    private string GetAllText()
    {
        return string.Join(" ", _body.Descendants<Text>().Select(t => t.Text));
    }
}
