// agent-notes: { ctx: "Composition tests: inline formatting inside tables, column width", deps: [Md2.Core, Md2.Parsing, Md2.Emit.Docx, DocumentFormat.OpenXml], state: active, last: "tara@2026-03-11" }

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Emit.Docx;
using Md2.Parsing;
using Shouldly;

namespace Md2.Integration.Tests;

/// <summary>
/// Tests that inline formatting features work correctly when nested inside
/// block-level containers (tables). These composition scenarios are where
/// individual-builder unit tests have blind spots — TableBuilder currently
/// strips all inline formatting via ExtractInlineText, so these tests
/// document the expected behavior that must be fixed.
/// </summary>
public class CompositionTests
{
    private async Task<(WordprocessingDocument Doc, MemoryStream Stream)> RunFullPipeline(string markdown)
    {
        var pipeline = new ConversionPipeline();
        var parserOptions = new ParserOptions();
        var doc = pipeline.Parse(markdown, parserOptions);

        pipeline.RegisterTransform(new YamlFrontMatterExtractor());
        var transformOptions = new TransformOptions();
        var transformResult = pipeline.Transform(doc, transformOptions);

        var theme = ResolvedTheme.CreateDefault();
        var emitOptions = new EmitOptions();
        var emitter = new DocxEmitter();
        var stream = new MemoryStream();

        await pipeline.Emit(transformResult.Document, theme, emitter, emitOptions, stream);
        stream.Position = 0;

        var wordDoc = WordprocessingDocument.Open(stream, false);
        return (wordDoc, stream);
    }

    /// <summary>
    /// Returns all TableCell elements from data rows (skipping the header row).
    /// </summary>
    private static List<TableCell> GetDataCells(Table table, int columnsPerRow)
    {
        return table.Descendants<TableCell>()
            .Skip(columnsPerRow) // skip header row
            .ToList();
    }

    #region Bold in Tables

    [Fact]
    public async Task Table_CellWithBold_ProducesBoldRun()
    {
        var markdown = @"
| Col A | Col B |
|-------|-------|
| plain | **bold text** |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        var dataCells = GetDataCells(table!, 2);

        var boldRuns = dataCells
            .SelectMany(c => c.Descendants<Run>())
            .Where(r => r.RunProperties?.Bold != null)
            .ToList();

        boldRuns.ShouldNotBeEmpty("Bold markdown in table data cells should produce Bold runs");

        var boldText = string.Join("", boldRuns.SelectMany(r => r.Descendants<Text>().Select(t => t.Text)));
        boldText.ShouldContain("bold text");
    }

    #endregion

    #region Italic in Tables

    [Fact]
    public async Task Table_CellWithItalic_ProducesItalicRun()
    {
        var markdown = @"
| Col A | Col B |
|-------|-------|
| plain | *italic text* |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        var dataCells = GetDataCells(table!, 2);

        var italicRuns = dataCells
            .SelectMany(c => c.Descendants<Run>())
            .Where(r => r.RunProperties?.Italic != null)
            .ToList();

        italicRuns.ShouldNotBeEmpty("Italic markdown in table data cells should produce Italic runs");

        var italicText = string.Join("", italicRuns.SelectMany(r => r.Descendants<Text>().Select(t => t.Text)));
        italicText.ShouldContain("italic text");
    }

    #endregion

    #region Strikethrough in Tables

    [Fact]
    public async Task Table_CellWithStrikethrough_ProducesStrikeRun()
    {
        var markdown = @"
| Style | Example |
|-------|---------|
| Strike | ~~removed~~ |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        var dataCells = GetDataCells(table!, 2);

        var strikeRuns = dataCells
            .SelectMany(c => c.Descendants<Run>())
            .Where(r => r.RunProperties?.Strike != null)
            .ToList();

        strikeRuns.ShouldNotBeEmpty("Strikethrough in table cells should produce Strike runs");

        var strikeText = string.Join("", strikeRuns.SelectMany(r => r.Descendants<Text>().Select(t => t.Text)));
        strikeText.ShouldContain("removed");
    }

    #endregion

    #region Inline Code in Tables

    [Fact]
    public async Task Table_CellWithInlineCode_ProducesCodeStyledRun()
    {
        var markdown = @"
| Col A | Col B |
|-------|-------|
| plain | `code here` |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        var dataCells = GetDataCells(table!, 2);

        // Inline code should have Shading on the run (the code background highlight)
        var codeRuns = dataCells
            .SelectMany(c => c.Descendants<Run>())
            .Where(r => r.RunProperties?.Shading != null)
            .ToList();

        codeRuns.ShouldNotBeEmpty("Inline code in table cells should produce runs with code shading");

        var codeText = string.Join("", codeRuns.SelectMany(r => r.Descendants<Text>().Select(t => t.Text)));
        codeText.ShouldContain("code here");
    }

    #endregion

    #region Links in Tables

    [Fact]
    public async Task Table_CellWithLink_ProducesHyperlinkElement()
    {
        var markdown = @"
| Col A | Col B |
|-------|-------|
| plain | [Click](https://example.com) |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        var hyperlinks = table!.Descendants<Hyperlink>().ToList();
        hyperlinks.ShouldNotBeEmpty("Links in table cells should produce Hyperlink elements");

        var linkText = string.Join("", hyperlinks.First().Descendants<Text>().Select(t => t.Text));
        linkText.ShouldBe("Click");
    }

    [Fact]
    public async Task Table_CellWithLink_HasValidHyperlinkRelationship()
    {
        var markdown = @"
| Col A | Col B |
|-------|-------|
| text  | [Example](https://example.com) |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var hyperlink = body.Descendants<Hyperlink>().FirstOrDefault();
        hyperlink.ShouldNotBeNull("Expected a hyperlink in the table");

        var relId = hyperlink!.Id?.Value;
        relId.ShouldNotBeNullOrEmpty("Hyperlink should have a relationship ID");

        var rel = wordDoc.MainDocumentPart!.HyperlinkRelationships
            .FirstOrDefault(r => r.Id == relId);
        rel.ShouldNotBeNull("Hyperlink relationship should exist in document");
        rel!.Uri.ToString().ShouldBe("https://example.com/");
    }

    #endregion

    #region Nested / Combined Formatting in Tables

    [Fact]
    public async Task Table_CellWithBoldItalic_ProducesBoldAndItalicRun()
    {
        var markdown = @"
| Col |
|-----|
| ***bold and italic*** |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        var dataCells = GetDataCells(table!, 1);

        // Should have a run that is BOTH bold and italic
        var boldItalicRuns = dataCells
            .SelectMany(c => c.Descendants<Run>())
            .Where(r => r.RunProperties?.Bold != null && r.RunProperties?.Italic != null)
            .ToList();

        boldItalicRuns.ShouldNotBeEmpty(
            "***text*** in table cells should produce runs with both Bold and Italic");
    }

    [Fact]
    public async Task Table_MixedInlineFormatting_AllFormatsPreserved()
    {
        var markdown = @"
| Inline Style   | Example                     |
|----------------|-----------------------------|
| Bold           | **This is bold**            |
| Italic         | *This is italic*            |
| Code           | `console.log(""hello"")`      |
| Link           | [Click here](https://x.com) |
| Strikethrough  | ~~removed~~                 |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        // Skip header row (2 cells per row)
        var dataCells = GetDataCells(table!, 2);
        var dataRuns = dataCells.SelectMany(c => c.Descendants<Run>()).ToList();

        // Each format type should be present somewhere in the data cells
        dataRuns.Any(r => r.RunProperties?.Bold != null)
            .ShouldBeTrue("Table should preserve bold formatting");

        dataRuns.Any(r => r.RunProperties?.Italic != null)
            .ShouldBeTrue("Table should preserve italic formatting");

        dataRuns.Any(r => r.RunProperties?.Shading != null)
            .ShouldBeTrue("Table should preserve inline code formatting");

        dataRuns.Any(r => r.RunProperties?.Strike != null)
            .ShouldBeTrue("Table should preserve strikethrough formatting");

        dataCells.SelectMany(c => c.Descendants<Hyperlink>()).Any()
            .ShouldBeTrue("Table should preserve hyperlinks");
    }

    [Fact]
    public async Task Table_CellWithMixedPlainAndFormatted_PreservesBothRuns()
    {
        var markdown = @"
| Col |
|-----|
| before **bold** after |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        var dataCells = GetDataCells(table!, 1);
        var runs = dataCells.SelectMany(c => c.Descendants<Run>()).ToList();

        // Should have at least two runs: the plain text and the bold text
        runs.Count.ShouldBeGreaterThanOrEqualTo(2,
            "Mixed plain and bold content should produce separate runs");

        // At least one run should NOT be bold (the plain text)
        runs.Any(r => r.RunProperties?.Bold == null)
            .ShouldBeTrue("Plain text portions should not be bold");

        // At least one run SHOULD be bold
        runs.Any(r => r.RunProperties?.Bold != null)
            .ShouldBeTrue("Bold portions should have Bold run property");
    }

    #endregion

    #region Strikethrough in Body Text (baseline)

    [Fact]
    public async Task BodyText_Strikethrough_ProducesStrikeRun()
    {
        var markdown = @"
Here is ~~strikethrough~~ text.
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var strikeRuns = body.Descendants<Run>()
            .Where(r => r.RunProperties?.Strike != null)
            .ToList();

        strikeRuns.ShouldNotBeEmpty("~~text~~ should produce a run with Strike property");

        var strikeText = string.Join("", strikeRuns.SelectMany(r => r.Descendants<Text>().Select(t => t.Text)));
        strikeText.ShouldContain("strikethrough");
    }

    #endregion

    #region Table Column Width

    [Fact]
    public async Task Table_AllColumnsRespectAlgorithmMinimumWidth()
    {
        // The TableBuilder enforces MinColumnFraction = 0.05 of available width.
        // Available width = PageWidth(11906) - MarginLeft(1800) - MarginRight(1800) = 8306 twips.
        // So minimum column width = 0.05 * 8306 = ~415 twips.
        // This test verifies the algorithm's own floor is applied, even for narrow
        // content like a 2-character "ID" column.
        var markdown = @"
| ID | Name                          | Description                                                        | Priority |
|----|-------------------------------|--------------------------------------------------------------------|----------|
| 1  | Short                         | A brief item                                                       | High     |
| 2  | Medium Length Name             | This description is somewhat longer to test column width heuristic | Medium   |
| 3  | A Rather Long Feature Name    | Short desc                                                         | Low      |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        var gridColumns = table!.GetFirstChild<TableGrid>()?.Elements<GridColumn>().ToList();
        gridColumns.ShouldNotBeNull("Table should have a TableGrid with column definitions");

        // The algorithm's minimum is 5% of available width (~415 twips).
        // Assert that no column is below this floor.
        const int algorithmMinTwips = 415;

        foreach (var col in gridColumns!)
        {
            var width = int.Parse(col.Width!);
            width.ShouldBeGreaterThanOrEqualTo(algorithmMinTwips,
                $"Column width {width} twips is below the algorithm's 5% floor of ~{algorithmMinTwips} twips");
        }
    }

    [Fact]
    public async Task Table_ShortColumnWordsNotWrapped()
    {
        // Columns with short single words like "Priority" should be wide enough
        // to fit without wrapping. At 11pt Cambria, ~100 twips/char is conservative.
        var markdown = @"
| ID | Name                          | Description                                                        | Priority |
|----|-------------------------------|--------------------------------------------------------------------|----------|
| 1  | Short                         | A brief item                                                       | High     |
| 2  | Medium Length Name             | This description is somewhat longer to test column width heuristic | Medium   |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        var gridColumns = table!.GetFirstChild<TableGrid>()?.Elements<GridColumn>().ToList();
        gridColumns.ShouldNotBeNull("Table should have a TableGrid with column definitions");

        // "Priority" is 8 chars. At ~100 twips/char (conservative for 11pt), need ~800 twips min.
        // Plus cell padding (57 twips x 2 = 114). So ~914 twips minimum.
        var lastColumnWidth = int.Parse(gridColumns!.Last().Width!);
        lastColumnWidth.ShouldBeGreaterThanOrEqualTo(900,
            $"'Priority' column at {lastColumnWidth} twips is too narrow — single words should not wrap");
    }

    [Fact]
    public async Task Table_ColumnWidthsSumToAvailableWidth()
    {
        var markdown = @"
| A | B | C |
|---|---|---|
| 1 | 2 | 3 |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        var gridColumns = table!.GetFirstChild<TableGrid>()?.Elements<GridColumn>().ToList();
        gridColumns.ShouldNotBeNull("Table should have a TableGrid with column definitions");

        var totalWidth = gridColumns!.Sum(c => int.Parse(c.Width!));

        // Available width = 11906 - 1800 - 1800 = 8306 twips
        const int expectedAvailableWidth = 8306;
        totalWidth.ShouldBe(expectedAvailableWidth,
            "Column widths should sum exactly to the available page width");
    }

    #endregion

    #region Table Cell Text Preservation

    [Fact]
    public async Task Table_FormattedCellText_NotLostDuringEmit()
    {
        // Regardless of whether formatting is applied, the text content itself
        // must not be lost. This catches the case where BuildRunFromInline
        // returns null for unsupported inline types, silently dropping content.
        var markdown = @"
| Feature | Status |
|---------|--------|
| **Bold** | *Italic* |
| `Code` | [Link](https://example.com) |
| ~~Strike~~ | plain |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull("Expected a table in the output");

        var allTableText = string.Join(" ",
            table!.Descendants<Text>().Select(t => t.Text));

        // All text content should survive, even if formatting is stripped
        allTableText.ShouldContain("Bold");
        allTableText.ShouldContain("Italic");
        allTableText.ShouldContain("Code");
        allTableText.ShouldContain("Link");
        allTableText.ShouldContain("Strike");
        allTableText.ShouldContain("plain");
    }

    #endregion

    #region Header Cell Formatting

    [Fact]
    public async Task Table_HeaderCellWithLink_HasWhiteColorAndBold()
    {
        var markdown = @"
| Name | [Link Header](https://example.com) |
|------|-------------------------------------|
| data | value                               |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull();

        // Get header row cells (first row)
        var headerCells = table!.Descendants<TableRow>().First()
            .Descendants<TableCell>().ToList();

        // The link header cell should have a hyperlink
        var hyperlinks = headerCells.SelectMany(c => c.Descendants<Hyperlink>()).ToList();
        hyperlinks.ShouldNotBeEmpty("Header cells should preserve hyperlinks");

        // The hyperlink run should have white color (for dark header background)
        var hyperlinkRuns = hyperlinks.SelectMany(h => h.Descendants<Run>()).ToList();
        hyperlinkRuns.ShouldNotBeEmpty();
        var color = hyperlinkRuns.First().RunProperties?.Color?.Val?.Value;
        color.ShouldBe("FFFFFF", "Header hyperlink text should be white on dark background");
    }

    [Fact]
    public async Task Table_HeaderCellWithItalic_PreservesItalicAndAddsBold()
    {
        var markdown = @"
| *Italic Header* |
|-----------------|
| data            |
";

        var (wordDoc, stream) = await RunFullPipeline(markdown);
        using var _ = wordDoc;
        using var __ = stream;

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var table = body.Elements<Table>().FirstOrDefault();
        table.ShouldNotBeNull();

        var headerCells = table!.Descendants<TableRow>().First()
            .Descendants<TableCell>().ToList();

        var headerRuns = headerCells.SelectMany(c => c.Descendants<Run>()).ToList();

        // Should have italic (from markdown) AND bold (from header override)
        var italicBoldRuns = headerRuns
            .Where(r => r.RunProperties?.Italic != null && r.RunProperties?.Bold != null)
            .ToList();

        italicBoldRuns.ShouldNotBeEmpty(
            "Header cells with *italic* should preserve italic and also apply bold");
    }

    #endregion
}
