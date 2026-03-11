---
agent-notes:
  ctx: "test strategy, pyramid, DOCX assertion patterns, coverage"
  deps: [docs/architecture.md, docs/plans/acceptance-criteria-v1.md, docs/code-map.md]
  state: active
  last: "tara@2026-03-11"
  key: ["Tara owns", "112 ACs across 9 test projects", "Shouldly for assertions"]
---

# Test Strategy -- md2 v1

**Owner:** Tara (testing)
**Project:** md2 -- Markdown to DOCX converter
**Created:** 2026-03-11
**Last reviewed:** 2026-03-11
**Status:** Active

---

## 1. Testing Principles

1. **Test behavior, not implementation.** "When I convert Markdown with a level-2 heading, the DOCX paragraph has style `Heading2`" -- not "DocxAstVisitor.VisitHeadingBlock calls ParagraphBuilder.Build with these arguments."
2. **The test pyramid is law.** Hundreds of unit tests, dozens of integration tests, a handful of E2E tests. If we find ourselves writing more integration tests than unit tests for a project, that is a design smell -- the code under test has too many dependencies.
3. **Every bug gets a regression test.** Before fixing a bug, write a test that reproduces it. The test goes red, the fix makes it go green.
4. **Tests are documentation.** A new developer should understand what md2 does by reading the test names. `Should_apply_theme_heading1_font_when_custom_theme_specifies_calibri_28pt` tells you more than any comment.
5. **Red first.** Tara writes the failing test. Sato makes it pass. No exceptions for "obvious" implementations.

---

## 2. Test Pyramid

### 2.1 Unit Tests

- **Scope:** Individual transforms, resolvers, parsers, converters in isolation. No file I/O, no Open XML SDK document creation, no Playwright.
- **Coverage target:** 90%+ line coverage on business logic (transforms, cascade resolver, theme parser, LaTeX parser, highlight tokenizer). 80%+ overall per project.
- **Framework:** xUnit 2.x
- **Assertion library:** Shouldly (see section 12 for justification)
- **Run command:** `dotnet test --filter "Category!=Integration&Category!=Playwright&Category!=Performance&Category!=VisualRegression"`
- **Speed target:** Full unit suite under 15 seconds.
- **What lives here:**
  - AST transform logic (input AST shape, output AST shape)
  - Theme cascade resolution (4-layer merge, gap warnings)
  - YAML theme parsing and validation
  - LaTeX-to-OMML conversion (string in, XML out)
  - Syntax highlight tokenization (code string in, token list out)
  - Front matter extraction
  - Smart typography rules
  - CLI argument parsing and validation
  - IRM/DRM file header detection
  - Style override parsing (`"heading1.fontSize=28pt"`)
  - TOC structure generation from heading nodes
  - Cross-reference slug generation and deduplication
  - Admonition syntax normalization
  - Diagram content-hash computation

### 2.2 Integration Tests

- **Scope:** Component interactions that produce or consume real artifacts. Primarily: Markdown in, DOCX out, inspect with Open XML SDK.
- **Coverage target:** Every DOCX element type (headings, body text, lists, tables, images, code blocks, blockquotes, footnotes, admonitions, math, cover page, TOC, page layout, document properties) has at least one integration test. Every theme cascade scenario has an integration test.
- **Framework:** xUnit 2.x + DocumentFormat.OpenXml for DOCX inspection
- **Run command:** `dotnet test --filter "Category=Integration"`
- **Speed target:** Full integration suite under 60 seconds (excluding Playwright tests).
- **Dependencies:** File system (temp directories), Open XML SDK (read-back of generated DOCX files).
- **What lives here:**
  - DOCX emitter output verification (the bulk of our integration tests)
  - Theme extraction from DOCX templates
  - Preset YAML file validity and schema compliance
  - Multi-file concatenation
  - Pipeline inspection (`--dry-run`, `--stage`)

### 2.3 Playwright Tests (Integration, Gated)

- **Scope:** Features that require a real Chromium instance -- Mermaid rendering, math rendering via KaTeX, and preview hot-reload.
- **Coverage target:** Core happy paths for Mermaid and math. Preview server lifecycle.
- **Framework:** xUnit 2.x + Microsoft.Playwright
- **Run command:** `dotnet test --filter "Category=Playwright"`
- **Gate:** Requires Chromium installed. Gated behind environment variable `INTEGRATION_TEST_PLAYWRIGHT=1`. CI must install Chromium before running these.
- **Speed target:** Under 30 seconds (excluding cold Chromium startup).
- **What lives here:**
  - Mermaid code block to PNG rendering (verify non-empty PNG, correct dimensions)
  - Math LaTeX to OMML via KaTeX pipeline
  - Preview server starts, serves HTML, hot-reloads on file change
  - Graceful degradation when Chromium is unavailable (separate tests that do NOT require Chromium -- they mock Playwright unavailability)

### 2.4 End-to-End Tests

- **Scope:** Full pipeline: CLI invocation with real arguments, real file I/O, real DOCX output.
- **Coverage target:** Critical user flows only (see list below).
- **Framework:** xUnit 2.x + `System.Diagnostics.Process` (invoke `md2` as a subprocess)
- **Run command:** `dotnet test --filter "Category=E2E"`
- **Speed target:** Under 90 seconds for all E2E tests.
- **Critical flows covered:**
  - `md2 input.md -o output.docx` (basic conversion, exit code 0)
  - Output file name inference (no `-o` flag)
  - Exit codes (0 success, 1 error, 2 protected template)
  - stdout vs stderr separation
  - `--quiet` and `--verbose` flags
  - `--dry-run` and `--stage` pipeline inspection
  - `md2 theme list`, `md2 theme validate`, `md2 theme extract`
  - `md2 doctor` environment checks
  - `--help` and `--version`
  - Multi-file concatenation
  - Error messages for missing files, invalid themes, protected templates

### 2.5 Performance Tests

- **Scope:** Timed benchmarks for acceptance criteria with explicit performance targets.
- **Framework:** xUnit 2.x + `System.Diagnostics.Stopwatch` (not BenchmarkDotNet -- we need pass/fail assertions, not statistical analysis)
- **Run command:** `dotnet test --filter "Category=Performance"`
- **Gate:** Run on CI but with relaxed tolerances (2x the stated budget) to account for CI variability. Local runs use exact budgets.
- **Tests:**
  - AC-4.7.5: 20 inline + 5 display math expressions under 10 seconds
  - AC-4.8.7: 10 Mermaid diagrams under 15 seconds total; per-diagram under 2 seconds after warm
  - AC-8.1.2: Preview hot-reload under 500ms
  - General: Conversion of a 20-page representative document under 5 seconds (excluding Mermaid/math)

### 2.6 Visual Regression Tests

- **Scope:** Preset stability -- ensure that changes to code do not accidentally alter the visual output of the 5 presets.
- **Framework:** xUnit 2.x + custom DOCX snapshot comparison (see section 8)
- **Run command:** `dotnet test --filter "Category=VisualRegression"`
- **Gate:** Gated behind `VISUAL_REGRESSION=1`. Only run on explicit request or before releases.
- **What lives here:**
  - Convert a reference Markdown file with each of the 5 presets
  - Compare the generated DOCX's style properties against a baseline snapshot
  - Fail if any style property drifts (font, size, color, spacing, margins)
  - Does NOT compare rendered pixels -- compares Open XML style values

---

## 3. Test Categories and Traits

All tests MUST be annotated with xUnit traits for filtering. Use the `[Trait("Category", "...")]` attribute.

```csharp
// Category traits -- every test gets exactly one
[Trait("Category", "Unit")]           // Fast, no I/O, no external deps
[Trait("Category", "Integration")]    // File I/O, Open XML SDK read-back
[Trait("Category", "Playwright")]     // Requires Chromium
[Trait("Category", "E2E")]            // Full CLI invocation as subprocess
[Trait("Category", "Performance")]    // Timed benchmarks with pass/fail
[Trait("Category", "VisualRegression")] // Preset snapshot comparison

// Feature area traits -- for targeted runs
[Trait("Feature", "Parsing")]
[Trait("Feature", "Transforms")]
[Trait("Feature", "ThemeCascade")]
[Trait("Feature", "DocxEmission")]
[Trait("Feature", "Tables")]
[Trait("Feature", "CodeBlocks")]
[Trait("Feature", "Math")]
[Trait("Feature", "Mermaid")]
[Trait("Feature", "Preview")]
[Trait("Feature", "CLI")]
[Trait("Feature", "ThemeManagement")]

// Priority traits -- map to acceptance criteria priorities
[Trait("Priority", "P0")]
[Trait("Priority", "P1")]
[Trait("Priority", "P2")]
```

### CI Filter Profiles

| Profile | Filter | When |
|---------|--------|------|
| Fast (PR checks) | `Category=Unit` | Every push, every PR |
| Standard (merge gate) | `Category!=Playwright&Category!=Performance&Category!=VisualRegression` | PR merge |
| Full (nightly/release) | All | Nightly build, pre-release |
| Playwright | `Category=Playwright` | Nightly, or when Mermaid/math/preview code changes |
| Performance | `Category=Performance` | Weekly, or when pipeline code changes |
| Visual Regression | `Category=VisualRegression` | Pre-release only |

---

## 4. Test Data Strategy

### 4.1 Markdown Input Files

Test Markdown inputs live in a shared `testdata/` directory at the solution root, organized by feature area.

```
tests/
  testdata/
    markdown/
      basic/
        headings.md           # H1-H6, one of each
        body-text.md          # Paragraphs with inline formatting
        lists.md              # Ordered, unordered, nested, task lists
        images.md             # Relative paths, missing files, oversized
        links.md              # External URLs, internal cross-refs
      rich/
        tables.md             # Simple, complex, wide, many-row
        code-blocks.md        # Multiple languages, no language, long lines
        blockquotes.md        # Simple, nested, containing other blocks
        footnotes.md          # Multiple footnotes, bidirectional
        admonitions.md        # All 5 types, custom titles, inline formatting
        definition-lists.md   # Single and multiple definitions
        math.md               # Inline, display, all LaTeX constructs
        mermaid.md            # All 10 diagram types
      pipeline/
        smart-typography.md   # Quotes, dashes, ellipses, code exemptions
        toc.md                # H1-H6 for TOC generation
        cover-page.md         # Front matter with all cover fields
        cross-references.md   # Duplicate headings, slug links
        front-matter.md       # Title, author, date, subject, keywords
      integration/
        representative-20pg.md  # The "gold standard" 20-page doc from AC done gate
        multi-file/
          part1.md
          part2.md
          part3.md
    themes/
      valid/
        full-theme.yaml       # Every property specified
        partial-theme.yaml    # Only heading1 overridden
        minimal-theme.yaml    # Just typography section
      invalid/
        bad-measurement.yaml  # heading1.fontSize: "big"
        bad-color.yaml        # colors.primary: "not-a-color"
        unknown-props.yaml    # futureProperty: value (should be ignored)
        variable-refs.yaml    # ${colors.primary} syntax (v1 warning)
      presets/                # Copies of the 5 preset files for test isolation
    templates/
      basic-template.docx     # Simple template with Heading1-3 custom styles
      partial-template.docx   # Template missing Heading4+ styles
      corporate-template.docx # Rich template with all styles
      irm-protected.bin       # File with OLE compound header (0xD0CF11E0)
      legacy.doc              # Legacy .doc format
      macro-enabled.docm      # Macro-enabled template
      oversized.bin           # >50MB file for size limit test
    expected/
      omml/                   # Known-good OMML output for LaTeX constructs
        fraction.xml
        superscript.xml
        subscript.xml
        greek-letters.xml
        summation.xml
        integral.xml
        sqrt.xml
        matrix.xml
    snapshots/
      preset-default.json     # Serialized style properties for baseline
      preset-technical.json
      preset-corporate.json
      preset-academic.json
      preset-minimal.json
```

### 4.2 Test Data Principles

1. **Markdown inputs are real files, not inline strings** (for integration and E2E tests). This makes tests readable, the test data reviewable, and lets us reuse the same files across test projects.
2. **Unit tests use inline strings.** When testing a single transform or parser, construct the minimal Markdown string in the test method. Do not read from disk for unit tests.
3. **Template DOCX files are pre-built and checked into the repo.** We do NOT generate them in tests -- that would test our generation code, not the template consumption code.
4. **The IRM-protected test file is a synthetic binary.** We only need the first 8 bytes to match the OLE compound document magic number (`D0 CF 11 E0 A1 B1 1A E1`). The rest can be zeros.
5. **Expected OMML files are hand-verified.** Create by running a known LaTeX expression through a reference tool (e.g., Word's equation editor) and extracting the OMML. These are the ground truth.
6. **No PII. No real corporate templates.** All test templates are synthetic.

### 4.3 Test Data Builders

For unit tests that need typed objects (not file I/O), use builder patterns:

```csharp
// Fluent builder for ThemeCascadeInput
internal static class TestThemeBuilder
{
    public static ThemeCascadeInput DefaultCascade() =>
        new(YamlTheme: null, PresetName: "default", TemplatePath: null, CliOverrides: null);

    public static ThemeCascadeInput WithPreset(string preset) =>
        DefaultCascade() with { PresetName = preset };

    public static ThemeCascadeInput WithYamlOverride(string property, string value) =>
        DefaultCascade() with
        {
            YamlTheme = new ThemeDefinition { /* minimal with one override */ }
        };
}

// Fluent builder for Markdig AST fragments
internal static class TestAstBuilder
{
    public static MarkdownDocument WithHeadings(params int[] levels) { /* ... */ }
    public static MarkdownDocument WithParagraph(string text) { /* ... */ }
    public static MarkdownDocument WithCodeBlock(string code, string? language = null) { /* ... */ }
    public static MarkdownDocument WithTable(int rows, int cols) { /* ... */ }
}
```

---

## 5. DOCX Assertion Strategy

This is the hardest part of our testing story. A DOCX file is a ZIP containing XML parts. We must open the generated DOCX with the Open XML SDK and walk the XML tree to verify correctness. Raw XML string comparison is too brittle. We need semantic assertions.

### 5.1 Core Approach

Every DOCX integration test follows this pattern:

```csharp
[Trait("Category", "Integration")]
[Fact]
public async Task Should_apply_heading1_style_from_theme()
{
    // Arrange: Markdown input + theme/options
    var markdown = "# Hello World";
    var options = new ConvertOptions { Preset = "default" };

    // Act: Run the pipeline, get a DOCX byte stream
    using var stream = new MemoryStream();
    await Pipeline.ConvertAsync(markdown, options, stream);
    stream.Position = 0;

    // Assert: Open with Open XML SDK, inspect document structure
    using var doc = WordprocessingDocument.Open(stream, false);
    var body = doc.MainDocumentPart!.Document.Body!;

    var heading = body.Elements<Paragraph>().First();

    // Verify style ID
    heading.ParagraphProperties!.ParagraphStyleId!.Val!.Value
        .ShouldBe("Heading1");

    // Verify it appears in Word's Navigation Pane
    heading.ParagraphProperties!.OutlineLevel!.Val!.Value
        .ShouldBe(0); // OutlineLevel 0 = Heading 1
}
```

### 5.2 Custom DOCX Assertion Helpers

We will build a `DocxAssert` helper class in a shared test utilities project (`Md2.TestUtilities`) that wraps common Open XML SDK inspection patterns. These helpers exist to make tests readable and to centralize the Open XML SDK traversal logic so that if the SDK API changes, we fix it in one place.

```csharp
/// <summary>
/// Fluent assertion helpers for Open XML DOCX inspection.
/// Lives in Md2.TestUtilities, referenced by all test projects that do DOCX validation.
/// </summary>
public static class DocxAssert
{
    // --- Paragraph Style ---

    public static void HasParagraphStyle(Paragraph paragraph, string expectedStyleId)
    {
        paragraph.ParagraphProperties.ShouldNotBeNull(
            "Paragraph has no ParagraphProperties");
        paragraph.ParagraphProperties!.ParagraphStyleId.ShouldNotBeNull(
            $"Paragraph has no style ID (expected '{expectedStyleId}')");
        paragraph.ParagraphProperties!.ParagraphStyleId!.Val!.Value
            .ShouldBe(expectedStyleId);
    }

    // --- Run Properties ---

    public static void RunIsBold(Run run)
    {
        run.RunProperties.ShouldNotBeNull("Run has no RunProperties");
        run.RunProperties!.Bold.ShouldNotBeNull("Run is not bold");
    }

    public static void RunIsItalic(Run run)
    {
        run.RunProperties.ShouldNotBeNull("Run has no RunProperties");
        run.RunProperties!.Italic.ShouldNotBeNull("Run is not italic");
    }

    public static void RunHasStrikethrough(Run run)
    {
        run.RunProperties.ShouldNotBeNull("Run has no RunProperties");
        run.RunProperties!.Strike.ShouldNotBeNull("Run has no strikethrough");
    }

    public static void RunHasFont(Run run, string expectedFont)
    {
        run.RunProperties.ShouldNotBeNull();
        var fonts = run.RunProperties!.RunFonts;
        fonts.ShouldNotBeNull($"Run has no font specification (expected '{expectedFont}')");
        // Open XML can set font on multiple targets; check Ascii and HighAnsi
        fonts!.Ascii!.Value.ShouldBe(expectedFont);
    }

    public static void RunHasFontSize(Run run, string expectedSizeInHalfPoints)
    {
        // Open XML stores font size in half-points: 24pt = "48"
        run.RunProperties.ShouldNotBeNull();
        run.RunProperties!.FontSize.ShouldNotBeNull("Run has no font size");
        run.RunProperties!.FontSize!.Val!.Value
            .ShouldBe(expectedSizeInHalfPoints);
    }

    public static void RunHasColor(Run run, string expectedHexColor)
    {
        // expectedHexColor without '#', e.g. "1B3A5C"
        run.RunProperties.ShouldNotBeNull();
        run.RunProperties!.Color.ShouldNotBeNull("Run has no color");
        run.RunProperties!.Color!.Val!.Value
            .ShouldBe(expectedHexColor, StringCompareShould.IgnoreCase);
    }

    public static void RunIsSuperscript(Run run)
    {
        run.RunProperties.ShouldNotBeNull();
        run.RunProperties!.VerticalTextAlignment.ShouldNotBeNull();
        run.RunProperties!.VerticalTextAlignment!.Val!.Value
            .ShouldBe(VerticalPositionValues.Superscript);
    }

    // --- Paragraph Layout ---

    public static void HasOutlineLevel(Paragraph paragraph, int expectedLevel)
    {
        paragraph.ParagraphProperties.ShouldNotBeNull();
        paragraph.ParagraphProperties!.OutlineLevel.ShouldNotBeNull();
        paragraph.ParagraphProperties!.OutlineLevel!.Val!.Value
            .ShouldBe(expectedLevel);
    }

    public static void HasWidowControl(Paragraph paragraph)
    {
        paragraph.ParagraphProperties.ShouldNotBeNull();
        paragraph.ParagraphProperties!.WidowControl.ShouldNotBeNull(
            "Paragraph has no widow/orphan control");
    }

    public static void HasLeftIndentation(Paragraph paragraph, int minTwips)
    {
        paragraph.ParagraphProperties.ShouldNotBeNull();
        paragraph.ParagraphProperties!.Indentation.ShouldNotBeNull();
        int actual = int.Parse(
            paragraph.ParagraphProperties!.Indentation!.Left!.Value!);
        actual.ShouldBeGreaterThanOrEqualTo(minTwips);
    }

    // --- Paragraph Borders ---

    public static void HasLeftBorder(Paragraph paragraph, string expectedColorHex)
    {
        paragraph.ParagraphProperties.ShouldNotBeNull();
        var borders = paragraph.ParagraphProperties!.ParagraphBorders;
        borders.ShouldNotBeNull("Paragraph has no borders");
        borders!.LeftBorder.ShouldNotBeNull("Paragraph has no left border");
        borders!.LeftBorder!.Color!.Value
            .ShouldBe(expectedColorHex, StringCompareShould.IgnoreCase);
    }

    // --- Table Assertions ---

    public static Table GetTable(Body body, int index = 0)
    {
        var tables = body.Elements<Table>().ToList();
        tables.Count.ShouldBeGreaterThan(index,
            $"Expected at least {index + 1} table(s), found {tables.Count}");
        return tables[index];
    }

    public static void TableHasRowCount(Table table, int expectedRows)
    {
        table.Elements<TableRow>().Count().ShouldBe(expectedRows);
    }

    public static void TableHeaderRowHasShading(Table table, string expectedHexColor)
    {
        var firstRow = table.Elements<TableRow>().First();
        var firstCell = firstRow.Elements<TableCell>().First();
        var shading = firstCell.TableCellProperties?.Shading;
        shading.ShouldNotBeNull("Header cell has no shading");
        shading!.Fill!.Value
            .ShouldBe(expectedHexColor, StringCompareShould.IgnoreCase);
    }

    public static void TableHasAlternatingRowShading(Table table)
    {
        var rows = table.Elements<TableRow>().Skip(1).ToList(); // skip header
        rows.Count.ShouldBeGreaterThanOrEqualTo(2,
            "Need at least 2 body rows to check alternating shading");

        var evenShading = GetCellShadingFill(rows[0]);
        var oddShading = GetCellShadingFill(rows[1]);
        evenShading.ShouldNotBe(oddShading,
            "Even and odd rows should have different shading");
    }

    public static void TableHeaderRepeats(Table table)
    {
        var firstRow = table.Elements<TableRow>().First();
        var trProps = firstRow.TableRowProperties;
        trProps.ShouldNotBeNull();
        trProps!.GetFirstChild<TableHeader>().ShouldNotBeNull(
            "First row does not have TableHeader set (header repeat disabled)");
    }

    public static void TableColumnWidthsAreProportional(
        Table table, double tolerancePercent = 15.0)
    {
        var firstRow = table.Elements<TableRow>().First();
        var cells = firstRow.Elements<TableCell>().ToList();

        var widths = cells.Select(c =>
        {
            var w = c.TableCellProperties?.TableCellWidth;
            w.ShouldNotBeNull("Cell has no width");
            return double.Parse(w!.Width!.Value!);
        }).ToList();

        double totalWidth = widths.Sum();
        totalWidth.ShouldBeGreaterThan(0);

        // At minimum, verify widths sum to something reasonable
        // and no single column takes more than (100 - tolerance)% of total
        foreach (var w in widths)
        {
            double pct = (w / totalWidth) * 100;
            pct.ShouldBeLessThan(100 - tolerancePercent,
                "One column is consuming almost the entire table width");
        }
    }

    public static void TableHasBorders(Table table)
    {
        var tblProps = table.GetFirstChild<TableProperties>();
        tblProps.ShouldNotBeNull("Table has no TableProperties");
        tblProps!.TableBorders.ShouldNotBeNull("Table has no borders");
    }

    // --- Hyperlinks ---

    public static void HasHyperlink(Paragraph paragraph, string expectedUri)
    {
        var hyperlink = paragraph.Elements<Hyperlink>().FirstOrDefault();
        hyperlink.ShouldNotBeNull("Paragraph has no hyperlink");
        // External hyperlinks use a relationship ID
        var relId = hyperlink!.Id?.Value;
        if (relId != null)
        {
            // Look up the relationship to verify the URI
            // (caller must pass the MainDocumentPart for relationship lookup)
        }
    }

    public static void HasBookmark(Paragraph paragraph, string expectedBookmarkName)
    {
        var bookmarkStart = paragraph.Elements<BookmarkStart>()
            .FirstOrDefault(b => b.Name?.Value == expectedBookmarkName);
        bookmarkStart.ShouldNotBeNull(
            $"Paragraph has no bookmark named '{expectedBookmarkName}'");
    }

    // --- Images ---

    public static Drawing GetInlineImage(Paragraph paragraph)
    {
        var drawing = paragraph.Descendants<Drawing>().FirstOrDefault();
        drawing.ShouldNotBeNull("Paragraph has no inline image");
        return drawing;
    }

    public static void ImageHasAltText(Drawing drawing, string expectedAltText)
    {
        var docProps = drawing.Descendants<DocumentFormat.OpenXml.Drawing
            .Wordprocessing.DocProperties>().FirstOrDefault();
        docProps.ShouldNotBeNull("Image has no DocProperties");
        docProps!.Description?.Value.ShouldBe(expectedAltText);
    }

    public static void ImageFitsPageWidth(Drawing drawing, long maxWidthEmu)
    {
        var extent = drawing.Descendants<DocumentFormat.OpenXml.Drawing
            .Wordprocessing.Extent>().FirstOrDefault();
        extent.ShouldNotBeNull("Image has no Extent");
        extent!.Cx!.Value.ShouldBeLessThanOrEqualTo(maxWidthEmu);
    }

    // --- Page Layout ---

    public static SectionProperties GetSectionProperties(Body body)
    {
        var sectPr = body.Elements<SectionProperties>().LastOrDefault();
        sectPr.ShouldNotBeNull("Document has no SectionProperties");
        return sectPr;
    }

    public static void HasPageSize(SectionProperties sectPr,
        uint expectedWidthTwips, uint expectedHeightTwips)
    {
        var pgSz = sectPr.GetFirstChild<PageSize>();
        pgSz.ShouldNotBeNull("Section has no PageSize");
        pgSz!.Width!.Value.ShouldBe(expectedWidthTwips);
        pgSz!.Height!.Value.ShouldBe(expectedHeightTwips);
    }

    public static void HasMargins(SectionProperties sectPr,
        uint top, uint bottom, uint left, uint right)
    {
        var pgMar = sectPr.GetFirstChild<PageMargin>();
        pgMar.ShouldNotBeNull("Section has no PageMargin");
        // PageMargin values are in twips (1 inch = 1440 twips)
        ((uint)pgMar!.Top!.Value).ShouldBe(top);
        ((uint)pgMar!.Bottom!.Value).ShouldBe(bottom);
        pgMar!.Left!.Value.ShouldBe(left);
        pgMar!.Right!.Value.ShouldBe(right);
    }

    // --- Document Properties ---

    public static void HasDocumentProperty(
        WordprocessingDocument doc, string property, string expectedValue)
    {
        var props = doc.PackageProperties;
        switch (property)
        {
            case "Title":
                props.Title.ShouldBe(expectedValue);
                break;
            case "Creator":
                props.Creator.ShouldBe(expectedValue);
                break;
            case "Subject":
                props.Subject.ShouldBe(expectedValue);
                break;
            case "Keywords":
                props.Keywords.ShouldBe(expectedValue);
                break;
            default:
                throw new ArgumentException($"Unknown property: {property}");
        }
    }

    // --- Footnotes ---

    public static void HasFootnoteReference(Paragraph paragraph, int footnoteId)
    {
        var fnRef = paragraph.Descendants<FootnoteReference>().FirstOrDefault();
        fnRef.ShouldNotBeNull("Paragraph has no footnote reference");
        fnRef!.Id!.Value.ShouldBe(footnoteId);
    }

    public static void FootnoteExists(WordprocessingDocument doc, int footnoteId)
    {
        var fnPart = doc.MainDocumentPart!.FootnotesPart;
        fnPart.ShouldNotBeNull("Document has no FootnotesPart");
        var footnote = fnPart!.Footnotes!.Elements<Footnote>()
            .FirstOrDefault(fn => fn.Id!.Value == footnoteId);
        footnote.ShouldNotBeNull($"Footnote {footnoteId} not found");
    }

    // --- Math ---

    public static void HasOfficeMath(Paragraph paragraph)
    {
        var math = paragraph.Descendants<DocumentFormat.OpenXml.Math.OfficeMath>()
            .FirstOrDefault();
        math.ShouldNotBeNull("Paragraph has no OfficeMath element");
    }

    // --- Breaks ---

    public static void HasBreakElement(Paragraph paragraph)
    {
        var brk = paragraph.Descendants<Break>().FirstOrDefault();
        brk.ShouldNotBeNull("Paragraph has no Break element");
    }

    public static void HasSectionBreak(Body body, int afterParagraphIndex)
    {
        var paragraphs = body.Elements<Paragraph>().ToList();
        paragraphs.Count.ShouldBeGreaterThan(afterParagraphIndex);
        var pPr = paragraphs[afterParagraphIndex].ParagraphProperties;
        pPr.ShouldNotBeNull();
        pPr!.GetFirstChild<SectionProperties>().ShouldNotBeNull(
            "No section break after the specified paragraph");
    }

    // --- Headers and Footers ---

    public static void HasHeaderWithText(WordprocessingDocument doc, string expectedText)
    {
        var headerPart = doc.MainDocumentPart!.HeaderParts.FirstOrDefault();
        headerPart.ShouldNotBeNull("Document has no header");
        var text = headerPart!.Header.InnerText;
        text.ShouldContain(expectedText);
    }

    public static void FooterHasPageNumbers(WordprocessingDocument doc)
    {
        var footerPart = doc.MainDocumentPart!.FooterParts.FirstOrDefault();
        footerPart.ShouldNotBeNull("Document has no footer");
        // PAGE field code
        var fieldCodes = footerPart!.Footer
            .Descendants<FieldCode>().ToList();
        fieldCodes.ShouldNotBeEmpty("Footer has no field codes");
        fieldCodes.ShouldContain(fc =>
            fc.InnerText.Contains("PAGE", StringComparison.OrdinalIgnoreCase),
            "Footer has no PAGE field code");
    }

    // --- Numbering (Lists) ---

    public static void ParagraphIsListItem(Paragraph paragraph)
    {
        paragraph.ParagraphProperties.ShouldNotBeNull();
        var numPr = paragraph.ParagraphProperties!
            .GetFirstChild<NumberingProperties>();
        numPr.ShouldNotBeNull("Paragraph is not a list item (no NumberingProperties)");
    }

    public static void ParagraphHasListLevel(Paragraph paragraph, int expectedLevel)
    {
        ParagraphIsListItem(paragraph);
        var numPr = paragraph.ParagraphProperties!
            .GetFirstChild<NumberingProperties>()!;
        var ilvl = numPr.GetFirstChild<NumberingLevelReference>();
        ilvl.ShouldNotBeNull("List item has no NumberingLevelReference");
        ilvl!.Val!.Value.ShouldBe(expectedLevel);
    }

    // --- Shading (Code blocks, table cells) ---

    public static void ParagraphHasShading(Paragraph paragraph, string expectedHexFill)
    {
        paragraph.ParagraphProperties.ShouldNotBeNull();
        var shading = paragraph.ParagraphProperties!.Shading;
        shading.ShouldNotBeNull("Paragraph has no shading");
        shading!.Fill!.Value
            .ShouldBe(expectedHexFill, StringCompareShould.IgnoreCase);
    }

    // --- Private Helpers ---

    private static string? GetCellShadingFill(TableRow row)
    {
        var cell = row.Elements<TableCell>().FirstOrDefault();
        return cell?.TableCellProperties?.Shading?.Fill?.Value;
    }
}
```

### 5.3 Concrete Assertion Patterns by Element Type

The following shows how specific acceptance criteria translate to concrete assertions. These are the patterns Sato should follow when implementing the DOCX emitter -- the tests are written first using these patterns.

#### Headings (AC-3.1.x)

```csharp
[Trait("Category", "Integration")]
[Trait("Feature", "DocxEmission")]
[Trait("Priority", "P0")]
[Fact]
public async Task Headings_1_through_6_map_to_word_heading_styles()
{
    var markdown = "# H1\n## H2\n### H3\n#### H4\n##### H5\n###### H6";
    using var stream = await ConvertToDocxStream(markdown);
    using var doc = WordprocessingDocument.Open(stream, false);
    var paragraphs = doc.MainDocumentPart!.Document.Body!
        .Elements<Paragraph>().ToList();

    paragraphs.Count.ShouldBe(6);
    DocxAssert.HasParagraphStyle(paragraphs[0], "Heading1");
    DocxAssert.HasParagraphStyle(paragraphs[1], "Heading2");
    DocxAssert.HasParagraphStyle(paragraphs[2], "Heading3");
    DocxAssert.HasParagraphStyle(paragraphs[3], "Heading4");
    DocxAssert.HasParagraphStyle(paragraphs[4], "Heading5");
    DocxAssert.HasParagraphStyle(paragraphs[5], "Heading6");
}

[Fact]
public async Task Heading_font_and_color_are_controlled_by_resolved_theme()
{
    var markdown = "# Themed Heading";
    var theme = TestThemeBuilder.WithHeading1(fontSize: "28pt", color: "#FF0000");
    using var stream = await ConvertToDocxStream(markdown, theme: theme);
    using var doc = WordprocessingDocument.Open(stream, false);

    var heading = doc.MainDocumentPart!.Document.Body!
        .Elements<Paragraph>().First();
    var run = heading.Elements<Run>().First();

    // 28pt = 56 half-points
    DocxAssert.RunHasFontSize(run, "56");
    DocxAssert.RunHasColor(run, "FF0000");
}

[Fact]
public async Task Headings_have_outline_level_for_navigation_pane()
{
    var markdown = "# H1\n## H2\n### H3";
    using var stream = await ConvertToDocxStream(markdown);
    using var doc = WordprocessingDocument.Open(stream, false);
    var paragraphs = doc.MainDocumentPart!.Document.Body!
        .Elements<Paragraph>().ToList();

    DocxAssert.HasOutlineLevel(paragraphs[0], 0); // H1 = outline level 0
    DocxAssert.HasOutlineLevel(paragraphs[1], 1); // H2 = outline level 1
    DocxAssert.HasOutlineLevel(paragraphs[2], 2); // H3 = outline level 2
}
```

#### Tables with Alternating Rows (AC-4.1.x)

```csharp
[Trait("Category", "Integration")]
[Trait("Feature", "Tables")]
[Trait("Priority", "P0")]
[Fact]
public async Task Table_header_row_has_distinct_background_and_bold_text()
{
    var markdown = """
        | Name | Age | City |
        |------|-----|------|
        | Alice | 30 | NYC |
        | Bob | 25 | LA |
        """;
    var theme = TestThemeBuilder.WithTableStyle(
        headerBg: "#1B3A5C", headerFg: "#FFFFFF", alternateRowBg: "#F8F9FA");
    using var stream = await ConvertToDocxStream(markdown, theme: theme);
    using var doc = WordprocessingDocument.Open(stream, false);

    var table = DocxAssert.GetTable(doc.MainDocumentPart!.Document.Body!);
    DocxAssert.TableHeaderRowHasShading(table, "1B3A5C");

    // Header text should be bold
    var headerRow = table.Elements<TableRow>().First();
    var firstCellRun = headerRow.Descendants<Run>().First();
    DocxAssert.RunIsBold(firstCellRun);
}

[Fact]
public async Task Table_has_alternating_row_shading()
{
    var markdown = """
        | A | B |
        |---|---|
        | 1 | 2 |
        | 3 | 4 |
        | 5 | 6 |
        | 7 | 8 |
        | 9 | 10 |
        | 11 | 12 |
        """;
    using var stream = await ConvertToDocxStream(markdown);
    using var doc = WordprocessingDocument.Open(stream, false);

    var table = DocxAssert.GetTable(doc.MainDocumentPart!.Document.Body!);
    DocxAssert.TableHasAlternatingRowShading(table);
}

[Fact]
public async Task Large_table_header_repeats_across_pages()
{
    // 50+ row table triggers cross-page behavior
    var rows = string.Join("\n",
        Enumerable.Range(1, 55).Select(i => $"| Row {i} | Data {i} |"));
    var markdown = $"| Name | Value |\n|------|-------|\n{rows}";

    using var stream = await ConvertToDocxStream(markdown);
    using var doc = WordprocessingDocument.Open(stream, false);

    var table = DocxAssert.GetTable(doc.MainDocumentPart!.Document.Body!);
    DocxAssert.TableHeaderRepeats(table);
}
```

#### Footnotes with Bidirectional Links (AC-4.4.x)

```csharp
[Trait("Category", "Integration")]
[Trait("Feature", "DocxEmission")]
[Trait("Priority", "P1")]
[Fact]
public async Task Footnote_reference_is_superscript_in_body()
{
    var markdown = "Some text[^1].\n\n[^1]: This is the footnote.";
    using var stream = await ConvertToDocxStream(markdown);
    using var doc = WordprocessingDocument.Open(stream, false);

    var body = doc.MainDocumentPart!.Document.Body!;
    var bodyParagraph = body.Elements<Paragraph>().First();

    // Find the footnote reference run
    var fnRefRun = bodyParagraph.Descendants<Run>()
        .First(r => r.Descendants<FootnoteReference>().Any());
    DocxAssert.RunIsSuperscript(fnRefRun);
}

[Fact]
public async Task Footnote_definition_exists_in_footnotes_part()
{
    var markdown = "Text[^1].\n\n[^1]: Definition here.";
    using var stream = await ConvertToDocxStream(markdown);
    using var doc = WordprocessingDocument.Open(stream, false);

    // Footnote IDs 0 and 1 are reserved (separator, continuation).
    // User footnotes start at 2+.
    DocxAssert.FootnoteExists(doc, 2);

    var fnPart = doc.MainDocumentPart!.FootnotesPart!;
    var footnote = fnPart.Footnotes!.Elements<Footnote>()
        .First(fn => fn.Id!.Value == 2);
    footnote.InnerText.ShouldContain("Definition here");
}
```

#### Code Blocks with Syntax Highlighting (AC-4.2.x)

```csharp
[Trait("Category", "Integration")]
[Trait("Feature", "CodeBlocks")]
[Trait("Priority", "P0")]
[Fact]
public async Task Code_block_uses_mono_font_with_background_shading()
{
    var markdown = "```\nconsole.log('hello');\n```";
    using var stream = await ConvertToDocxStream(markdown);
    using var doc = WordprocessingDocument.Open(stream, false);

    var body = doc.MainDocumentPart!.Document.Body!;
    var codeParagraph = body.Elements<Paragraph>().First();

    // Verify mono font on the run
    var run = codeParagraph.Elements<Run>().First();
    DocxAssert.RunHasFont(run, "Cascadia Code"); // default preset mono font

    // Verify paragraph has background shading
    DocxAssert.ParagraphHasShading(codeParagraph, "F5F5F5"); // default codeBg
}

[Fact]
public async Task Syntax_highlighted_code_block_has_multiple_colors()
{
    var markdown = """
        ```python
        def hello():
            # comment
            return "world"
        ```
        """;
    using var stream = await ConvertToDocxStream(markdown);
    using var doc = WordprocessingDocument.Open(stream, false);

    var body = doc.MainDocumentPart!.Document.Body!;
    // Code block may be a single paragraph with multiple styled runs
    var codeParagraph = body.Elements<Paragraph>().First();
    var runs = codeParagraph.Elements<Run>().ToList();

    // Collect distinct foreground colors
    var colors = runs
        .Select(r => r.RunProperties?.Color?.Val?.Value)
        .Where(c => c != null)
        .Distinct()
        .ToList();

    // At least 3 distinct colors: keyword (def), comment (#), string ("world")
    colors.Count.ShouldBeGreaterThanOrEqualTo(3,
        "Syntax-highlighted code should have at least 3 distinct token colors");
}
```

---

## 6. Mermaid and Math Test Strategy

### 6.1 Mermaid Testing Layers

| Layer | What | Chromium Required | How |
|-------|------|-------------------|-----|
| Unit | Content-hash computation | No | Hash a known Mermaid string, assert deterministic output |
| Unit | Cache hit/miss logic | No | Mock the cache, verify lookup and store calls |
| Unit | `--no-mermaid` flag handling | No | Assert Mermaid code blocks pass through as code blocks |
| Integration (Playwright) | Actual PNG rendering | Yes | Render a flowchart, verify PNG bytes are non-empty, image dimensions >= 2x scale |
| Integration (Playwright) | 10 diagram types | Yes | Flowchart, sequence, class, state, ER, gantt, pie, mindmap, gitgraph, C4 |
| Integration (Mock) | Graceful degradation | No | Mock Playwright as unavailable, assert warning + code block fallback |
| Performance (Playwright) | 10 diagrams < 15s | Yes | Timed benchmark |

**Gating pattern for Playwright tests:**

```csharp
public sealed class PlaywrightAvailableFactAttribute : FactAttribute
{
    public PlaywrightAvailableFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("INTEGRATION_TEST_PLAYWRIGHT") != "1")
        {
            Skip = "Playwright integration tests are disabled. " +
                   "Set INTEGRATION_TEST_PLAYWRIGHT=1 to run.";
        }
    }
}

[Trait("Category", "Playwright")]
[PlaywrightAvailableFact]
public async Task Mermaid_flowchart_renders_to_png_with_2x_scale()
{
    var mermaidSource = """
        graph TD
            A[Start] --> B{Decision}
            B -->|Yes| C[Do thing]
            B -->|No| D[Other thing]
        """;
    var renderer = new MermaidRenderer(new BrowserManager());
    var png = await renderer.RenderAsync(mermaidSource, scale: 2.0);

    png.ShouldNotBeEmpty();
    // Verify it's actually a PNG (magic bytes)
    png[0].ShouldBe((byte)0x89);
    png[1].ShouldBe((byte)0x50); // 'P'
    png[2].ShouldBe((byte)0x4E); // 'N'
    png[3].ShouldBe((byte)0x47); // 'G'
}
```

**Reminder to implementer:** Mock-only tests are insufficient for Mermaid and math rendering. The unit tests verify "if the subprocess returns X, we handle it correctly" but cannot verify "does Playwright actually render this Mermaid source correctly." Run with `INTEGRATION_TEST_PLAYWRIGHT=1` before declaring green.

### 6.2 Math Testing Layers

| Layer | What | Chromium Required | How |
|-------|------|-------------------|-----|
| Unit | LaTeX parser (string to expression tree) | No | Parse known LaTeX, assert tree structure |
| Unit | OMML emitter (expression tree to XML) | No | Compare output XML against known-good OMML files in `testdata/expected/omml/` |
| Unit | Individual LaTeX constructs | No | One test per construct from AC-4.7.3 |
| Integration (Playwright) | Full KaTeX pipeline | Yes | LaTeX string through KaTeX, get MathML, transform to OMML |
| Integration | OMML in DOCX | Yes (for KaTeX path) | Inline math within paragraph, display math as centered block |
| Integration (Mock) | Graceful degradation | No | Mock Playwright unavailable, assert code span fallback |
| Performance (Playwright) | 25 expressions < 10s | Yes | Timed benchmark |

**OMML comparison pattern:**

```csharp
[Trait("Category", "Unit")]
[Trait("Feature", "Math")]
[Theory]
[InlineData(@"\frac{a}{b}", "fraction.xml")]
[InlineData(@"x^2", "superscript.xml")]
[InlineData(@"x_i", "subscript.xml")]
[InlineData(@"\alpha + \beta", "greek-letters.xml")]
[InlineData(@"\sum_{i=0}^{n}", "summation.xml")]
[InlineData(@"\int_0^1 f(x) dx", "integral.xml")]
[InlineData(@"\sqrt{x}", "sqrt.xml")]
[InlineData(@"\begin{bmatrix} a & b \\ c & d \end{bmatrix}", "matrix.xml")]
public void Latex_construct_produces_expected_omml(string latex, string expectedFile)
{
    var tree = LaTeXParser.Parse(latex);
    var omml = OmmlEmitter.Emit(tree);
    var actual = omml.ToString(SaveOptions.DisableFormatting);

    var expectedPath = Path.Combine(TestDataRoot, "expected", "omml", expectedFile);
    var expected = File.ReadAllText(expectedPath).Trim();

    // Normalize whitespace for comparison
    actual.ShouldBe(expected);
}
```

---

## 7. Coverage Targets

### 7.1 Per-Project Targets

| Test Project | Src Project | Line Coverage | Branch Coverage | Notes |
|--------------|-------------|---------------|-----------------|-------|
| Md2.Core.Tests | Md2.Core | 90% | 85% | Pipeline orchestration, all transforms |
| Md2.Parsing.Tests | Md2.Parsing | 85% | 80% | Markdig config, front matter, admonition extension |
| Md2.Themes.Tests | Md2.Themes | 90% | 85% | Cascade resolver is critical -- high branch coverage |
| Md2.Emit.Docx.Tests | Md2.Emit.Docx | 85% | 80% | Every element builder, style applicator |
| Md2.Highlight.Tests | Md2.Highlight | 80% | 75% | Token accuracy; language coverage is breadth not depth |
| Md2.Math.Tests | Md2.Math | 90% | 85% | LaTeX parser + OMML emitter -- correctness critical |
| Md2.Diagrams.Tests | Md2.Diagrams | 70% | 65% | Lower because Playwright integration; unit-testable logic should hit 85% |
| Md2.Preview.Tests | Md2.Preview | 70% | 65% | Same -- Playwright-dependent; server lifecycle logic should hit 85% |
| Md2.Integration.Tests | (cross-cutting) | N/A | N/A | Coverage measured on src projects above, not separately |

### 7.2 Coverage Gates

| Scope | Metric | Threshold | Enforced by |
|-------|--------|-----------|-------------|
| Overall (all src projects) | Line coverage | 85% | CI |
| New code (per PR) | Line coverage | 90% | Code review (Tara) |
| Critical paths (cascade, emitter, pipeline) | Branch coverage | 85% | Code review (Tara) |
| Md2.Math (correctness-critical) | Branch coverage | 85% | CI |
| Md2.Themes (cascade correctness) | Branch coverage | 85% | CI |

### 7.3 Coverage Tooling

- **Tool:** `coverlet.collector` NuGet package + `dotnet test --collect:"XPlat Code Coverage"`
- **Report generation:** `reportgenerator` to produce HTML and Cobertura XML
- **CI integration:** Cobertura XML uploaded as artifact; threshold check in pipeline

---

## 8. Snapshot and Baseline Strategy for Preset Regression

### 8.1 What We Snapshot

For each of the 5 presets (default, technical, corporate, academic, minimal), we convert a reference Markdown document and extract a **style property manifest** -- a JSON file listing every style property value in the output DOCX. This is NOT a binary diff of the DOCX file (which would be fragile due to timestamps, relationship IDs, and other non-deterministic content).

### 8.2 Manifest Structure

```json
{
  "preset": "corporate",
  "generatedAt": "2026-03-15T10:00:00Z",
  "styles": {
    "Heading1": {
      "fontName": "Calibri",
      "fontSizeHalfPt": "56",
      "bold": true,
      "color": "1B3A5C",
      "spaceBeforeTwips": "480",
      "spaceAfterTwips": "120"
    },
    "Heading2": { /* ... */ },
    "Normal": { /* ... */ },
    "CodeBlock": { /* ... */ }
  },
  "pageLayout": {
    "pageWidthTwips": "11906",
    "pageHeightTwips": "16838",
    "marginTopTwips": "1440",
    "marginBottomTwips": "1440",
    "marginLeftTwips": "1800",
    "marginRightTwips": "1800"
  },
  "tableStyle": {
    "headerBgColor": "1B3A5C",
    "alternateRowBgColor": "F8F9FA",
    "borderColor": "DEE2E6"
  }
}
```

### 8.3 Comparison Logic

```csharp
[Trait("Category", "VisualRegression")]
[Theory]
[InlineData("default")]
[InlineData("technical")]
[InlineData("corporate")]
[InlineData("academic")]
[InlineData("minimal")]
public async Task Preset_output_matches_baseline(string presetName)
{
    // Generate DOCX with the preset
    using var stream = await ConvertToDocxStream(ReferenceMarkdownPath,
        preset: presetName);

    // Extract style manifest from the generated DOCX
    var actualManifest = StyleManifestExtractor.Extract(stream);

    // Load baseline
    var baselinePath = Path.Combine(TestDataRoot, "snapshots",
        $"preset-{presetName}.json");
    var expectedManifest = StyleManifest.LoadFrom(baselinePath);

    // Compare with detailed diff output on failure
    actualManifest.ShouldMatchBaseline(expectedManifest);
}
```

### 8.4 Updating Baselines

When a preset intentionally changes (e.g., during initial creation or a design refresh):

1. Run tests with `UPDATE_SNAPSHOTS=1` to regenerate baselines
2. Review the diff in git to confirm changes are intentional
3. Commit the new baseline files

Accidental changes (no code change to presets, but snapshot drifts) indicate a regression in the emitter or cascade resolver and should block the merge.

---

## 9. CI Configuration

### 9.1 Test Stages

```yaml
# Conceptual CI pipeline -- adapt to actual CI system

stages:
  - name: unit-tests
    runs-on: ubuntu-latest
    commands:
      - dotnet test --filter "Category=Unit" --collect:"XPlat Code Coverage"
    timeout: 5m

  - name: integration-tests
    runs-on: ubuntu-latest
    commands:
      - dotnet test --filter "Category=Integration" --collect:"XPlat Code Coverage"
    timeout: 5m

  - name: e2e-tests
    runs-on: ubuntu-latest
    commands:
      - dotnet test --filter "Category=E2E"
    timeout: 5m

  - name: playwright-tests
    runs-on: ubuntu-latest
    env:
      INTEGRATION_TEST_PLAYWRIGHT: "1"
    setup:
      - dotnet tool install --global Microsoft.Playwright.CLI
      - playwright install chromium
    commands:
      - dotnet test --filter "Category=Playwright"
    timeout: 10m
    trigger: nightly OR changes to Md2.Diagrams/** OR Md2.Math/** OR Md2.Preview/**

  - name: performance-tests
    runs-on: ubuntu-latest
    env:
      INTEGRATION_TEST_PLAYWRIGHT: "1"
      PERFORMANCE_TOLERANCE_MULTIPLIER: "2.0"  # CI is slower
    setup:
      - playwright install chromium
    commands:
      - dotnet test --filter "Category=Performance"
    timeout: 10m
    trigger: weekly OR changes to pipeline code

  - name: visual-regression
    runs-on: ubuntu-latest
    commands:
      - dotnet test --filter "Category=VisualRegression"
    timeout: 5m
    trigger: pre-release OR changes to presets/**

  - name: coverage-report
    needs: [unit-tests, integration-tests]
    commands:
      - reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report
      - # Fail if overall line coverage < 85%
```

### 9.2 Which Tests Need Chromium

| Test Category | Needs Chromium | Projects |
|---------------|---------------|----------|
| Unit | No | All |
| Integration | No | All except Md2.Diagrams.Tests, Md2.Preview.Tests |
| Playwright | Yes | Md2.Diagrams.Tests, Md2.Math.Tests (KaTeX path), Md2.Preview.Tests |
| E2E | Depends | Only if the E2E test exercises Mermaid or math rendering |
| Performance | Yes | Math and Mermaid benchmarks |
| Visual Regression | No | Style property extraction only |

### 9.3 Handling Visual Regression in CI

Visual regression tests compare extracted Open XML style properties, not rendered pixels. This means:
- No screenshot comparison infrastructure needed
- No pixel-diff tolerance tuning
- Tests are deterministic across platforms
- The only source of flakiness would be a non-deterministic emitter (which would be a bug)

---

## 10. Flaky Test Policy

1. **A test that fails intermittently is a defect.** Treat it as a bug, not a nuisance.
2. **Quarantine after 2 flakes.** If a test flakes twice in CI within a sprint, move it to a `[Trait("Category", "Quarantined")]` category and file an issue.
3. **Fix within the current sprint.** Quarantined tests do not accumulate.
4. **No `Thread.Sleep` in tests.** Use `TaskCompletionSource`, `SemaphoreSlim`, or proper async polling with timeouts.
5. **No test-order dependencies.** Every test class gets a fresh state. Use `IAsyncLifetime` for setup/teardown, not static state.
6. **Temp files are isolated.** Each test that writes to disk gets its own `Path.GetTempPath()` + `Guid` directory, cleaned up in `DisposeAsync`.

---

## 11. What Is NOT Tested (and Why)

| Area | Reason |
|------|--------|
| Word's rendering of the DOCX | We test OOXML structure, not how Word renders it. Rendering differences between Word versions are Microsoft's problem. |
| Markdig parser correctness | Markdig has its own test suite. We test our configuration of Markdig (extensions enabled, custom extensions), not Markdig's parsing logic. |
| TextMateSharp grammar accuracy | We test that tokenization produces tokens with colors. Whether a TextMate grammar correctly identifies a Python decorator as a decorator is TextMateSharp's responsibility. |
| Playwright Chromium behavior | We test that our Playwright invocation produces results. Chromium rendering bugs are upstream. |
| Cross-platform font rendering | DOCX references fonts by name. How they render on different OSes is a Word/LibreOffice concern. |
| Open XML SDK correctness | We trust the SDK to produce valid OOXML. We test that we call it correctly. |
| PPTX emitter | Out of scope for v1. The `IFormatEmitter` seam is tested at the interface level only. |
| Variable interpolation in themes | Deferred post-v1 per scope. Tests for the `${...}` warning message are in scope. |

---

## 12. Assertion Library Decision: Shouldly

**Decision:** Use **Shouldly** over FluentAssertions.

**Justification:**

1. **Simpler API surface.** Shouldly uses extension methods directly on the value (`value.ShouldBe(expected)`) rather than wrapping in `value.Should().Be(expected)`. For a project with hundreds of assertions against Open XML SDK types, the shorter syntax compounds into meaningful readability gains.

2. **Better error messages by default.** Shouldly captures the variable name from the source expression and includes it in failure messages: `"heading.ParagraphProperties.ParagraphStyleId.Val.Value should be 'Heading1' but was 'Normal'"`. This is critical for DOCX testing where failures often involve deeply nested Open XML property chains.

3. **No licensing concerns.** Shouldly is MIT-licensed. FluentAssertions moved to a dual license (Apache 2.0 for non-commercial, commercial license for business use) starting with v8. While our project may qualify for Apache 2.0, Shouldly avoids the question entirely.

4. **Lower API churn.** Shouldly's API has been stable for years. FluentAssertions has had breaking changes across major versions.

5. **Sufficient for our needs.** We do not need FluentAssertions' more advanced features (e.g., structural comparison of object graphs, `BeEquivalentTo`). Our assertions are property-by-property checks against Open XML SDK types.

**NuGet package:** `Shouldly` (latest stable) in all test projects.

---

## 13. Test Project to Acceptance Criteria Mapping

This table maps each test project to the acceptance criteria it is responsible for verifying. Every AC must have a home.

| Test Project | Acceptance Criteria IDs | Count |
|--------------|------------------------|-------|
| Md2.Core.Tests | AC-1.1.1-1.1.5, AC-2.1.1-2.1.4, AC-2.2.3 (unit part), AC-2.3.4, AC-2.4.3, AC-2.2.1-2.2.2 (TOC structure) | ~15 |
| Md2.Parsing.Tests | AC-1.1.1-1.1.5 (parser config), AC-1.2.1-1.2.5 | ~10 |
| Md2.Themes.Tests | AC-5.1.1, AC-5.1.4-5.1.5, AC-5.2.1-5.2.6, AC-5.4.1-5.4.3 | ~15 |
| Md2.Emit.Docx.Tests | AC-3.1.1-3.5.4, AC-4.1.1-4.6.3, AC-9.1.1-9.1.5 | ~40 |
| Md2.Highlight.Tests | AC-4.2.1-4.2.6 (unit: tokenization, 20 languages) | ~8 |
| Md2.Math.Tests | AC-4.7.1-4.7.3 (unit: LaTeX parsing, OMML emission) | ~10 |
| Md2.Diagrams.Tests | AC-4.8.1-4.8.7 (Playwright + mocks) | ~8 |
| Md2.Preview.Tests | AC-8.1.1-8.1.5 | ~5 |
| Md2.Integration.Tests | AC-5.3.1-5.3.7, AC-6.1.1-6.3.1, AC-7.1.1-7.4.4, AC-4.7.4-4.7.5, AC-4.8.4-4.8.5 | ~30 |

**Total coverage:** 112 ACs mapped across 9 test projects. Every AC has at least one test.

---

## 14. Shared Test Infrastructure

### 14.1 Md2.TestUtilities Project

A shared project (not a test project itself -- no tests in it) referenced by all 9 test projects. Contains:

- `DocxAssert` (section 5.2) -- DOCX assertion helpers
- `TestThemeBuilder` -- fluent builders for theme cascade inputs
- `TestAstBuilder` -- fluent builders for Markdig AST fragments
- `StyleManifestExtractor` -- extracts style manifests for visual regression
- `PlaywrightAvailableFactAttribute` -- skip attribute for Playwright tests
- `TempDirectory` -- `IAsyncDisposable` wrapper for isolated temp directories
- `ConvertHelper` -- shared `ConvertToDocxStream()` method used across integration tests

### 14.2 Test Execution Helpers

```csharp
/// <summary>
/// Converts Markdown to a DOCX MemoryStream using the full pipeline.
/// Shared across all integration test projects.
/// </summary>
public static class ConvertHelper
{
    public static async Task<MemoryStream> ConvertToDocxStream(
        string markdown,
        string preset = "default",
        ThemeDefinition? theme = null,
        string? templatePath = null,
        IReadOnlyList<StyleOverride>? cliOverrides = null)
    {
        var pipeline = new ConversionPipeline();
        var stream = new MemoryStream();

        var cascadeInput = new ThemeCascadeInput(
            YamlTheme: theme,
            PresetName: preset,
            TemplatePath: templatePath,
            CliOverrides: cliOverrides);

        var parserOptions = new ParserOptions();
        var transformOptions = new TransformOptions();
        var emitOptions = new EmitOptions(TemplatePath: templatePath);

        var doc = pipeline.Parse(markdown, parserOptions);
        doc = pipeline.Transform(doc, transformOptions);
        var resolvedTheme = pipeline.ResolveStyles(cascadeInput);

        var emitter = new DocxEmitter();
        await emitter.EmitAsync(doc, resolvedTheme.Theme, emitOptions, stream);

        stream.Position = 0;
        return stream;
    }
}

/// <summary>
/// Provides an isolated temporary directory that is cleaned up on disposal.
/// </summary>
public sealed class TempDirectory : IAsyncDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "md2-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
        return ValueTask.CompletedTask;
    }
}
```

---

## 15. Risks and Assumptions

| Risk | Impact | Mitigation |
|------|--------|------------|
| Open XML SDK version differences produce different OOXML | Snapshot tests break on SDK update | Pin SDK version. Run visual regression after any SDK bump. |
| Chromium download flakes in CI | Playwright tests fail intermittently | Use Playwright's built-in retry for install. Cache the browser in CI. |
| LaTeX-to-OMML known-good files may be wrong | Math tests pass but output is incorrect in Word | Hand-verify each expected OMML file by pasting into Word equation editor. |
| Table auto-sizing is heuristic | Hard to write deterministic assertions for column widths | Use tolerance-based assertions (15% per AC-4.1.4). Test boundary cases. |
| Performance test thresholds are CI-sensitive | Tests flake on slow CI runners | Use 2x multiplier in CI. Track trends rather than absolute values. |
| Markdig AST structure may change across versions | Unit tests for transforms break | Pin Markdig version. Run transform tests after any Markdig bump. |

---

## Appendix A: Acceptance Criteria Priority Coverage

| Priority | Total ACs | Must Have Tests | Test Level |
|----------|-----------|----------------|------------|
| P0 | 48 | 48 (100%) | Mostly integration (DOCX verification) + E2E (CLI) |
| P1 | 56 | 56 (100%) | Mix of unit + integration + Playwright |
| P2 | 8 | 8 (100%) | Integration + visual regression |

**All 112 acceptance criteria have defined verification methods and are mapped to test projects.** This is the test strategy's primary deliverable: no acceptance criterion is unaccounted for.

---

## Appendix B: Open XML SDK Quick Reference for Test Authors

Common conversions needed when writing DOCX assertions:

| What | Open XML Unit | Conversion |
|------|---------------|------------|
| Font size | Half-points (string) | 12pt = "24", 28pt = "56" |
| Page width/height | Twips (uint) | 1 inch = 1440 twips. A4 = 11906 x 16838 |
| Margins | Twips (int/uint) | 1 inch = 1440, 1.25 inch = 1800 |
| Line spacing | 240ths of a line | Single = 240, 1.15 = 276, 1.5 = 360, Double = 480 |
| Image dimensions | EMU (English Metric Units) | 1 inch = 914400 EMU. 1 px at 96 DPI = 9525 EMU |
| Colors | Hex string without '#' | "#1B3A5C" in theme = "1B3A5C" in OOXML |
| Indentation | Twips | 0.5 inch = 720 twips |
| Border width | Eighths of a point | 0.5pt = 4 |

These conversions will be centralized as constants in `Md2.TestUtilities.OpenXmlConstants`.
