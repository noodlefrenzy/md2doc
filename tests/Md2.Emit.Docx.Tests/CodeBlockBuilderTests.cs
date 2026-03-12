// agent-notes: { ctx: "Tests for CodeBlockBuilder: mono font, background, borders, line wrapping", deps: [Md2.Emit.Docx.CodeBlockBuilder, DocumentFormat.OpenXml], state: active, last: "tara@2026-03-12" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class CodeBlockBuilderTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();

    [Fact]
    public void Build_SimpleCode_ReturnsTable()
    {
        var builder = new CodeBlockBuilder(new ParagraphBuilder(_theme));
        var code = "Console.WriteLine(\"Hello\");";

        var result = builder.Build(code, null, _theme);

        result.ShouldNotBeNull();
        result.ShouldBeOfType<Table>();
    }

    [Fact]
    public void Build_SimpleCode_HasSingleCell()
    {
        var builder = new CodeBlockBuilder(new ParagraphBuilder(_theme));
        var code = "var x = 1;";

        var table = builder.Build(code, null, _theme);

        var rows = table.Elements<TableRow>().ToList();
        rows.Count.ShouldBe(1);
        var cells = rows[0].Elements<TableCell>().ToList();
        cells.Count.ShouldBe(1);
    }

    [Fact]
    public void Build_MultilineCode_HasOneParagraphPerLine()
    {
        var builder = new CodeBlockBuilder(new ParagraphBuilder(_theme));
        var code = "line1\nline2\nline3";

        var table = builder.Build(code, null, _theme);

        var cell = table.Descendants<TableCell>().First();
        var paragraphs = cell.Elements<Paragraph>().ToList();
        paragraphs.Count.ShouldBe(3);
    }

    [Fact]
    public void Build_UsesMonoFont()
    {
        var builder = new CodeBlockBuilder(new ParagraphBuilder(_theme));
        var code = "hello";

        var table = builder.Build(code, null, _theme);

        var run = table.Descendants<Run>().First();
        var fonts = run.RunProperties?.RunFonts;
        fonts.ShouldNotBeNull();
        fonts!.Ascii!.Value.ShouldBe(_theme.MonoFont);
    }

    [Fact]
    public void Build_HasBackgroundShading()
    {
        var builder = new CodeBlockBuilder(new ParagraphBuilder(_theme));
        var code = "x = 1";

        var table = builder.Build(code, null, _theme);

        var cell = table.Descendants<TableCell>().First();
        var shading = cell.TableCellProperties?.Shading;
        shading.ShouldNotBeNull();
        shading!.Fill!.Value.ShouldBe(_theme.CodeBackgroundColor);
    }

    [Fact]
    public void Build_HasBorders()
    {
        var builder = new CodeBlockBuilder(new ParagraphBuilder(_theme));
        var code = "x = 1";

        var table = builder.Build(code, null, _theme);

        var tableProps = table.GetFirstChild<TableProperties>();
        tableProps.ShouldNotBeNull();
        var borders = tableProps!.TableBorders;
        borders.ShouldNotBeNull();
        borders!.TopBorder.ShouldNotBeNull();
        borders!.BottomBorder.ShouldNotBeNull();
        borders!.LeftBorder.ShouldNotBeNull();
        borders!.RightBorder.ShouldNotBeNull();
    }

    [Fact]
    public void Build_PreservesWhitespace()
    {
        var builder = new CodeBlockBuilder(new ParagraphBuilder(_theme));
        var code = "  indented";

        var table = builder.Build(code, null, _theme);

        var text = table.Descendants<Text>().First();
        text.Text.ShouldBe("  indented");
        text.Space!.Value.ShouldBe(SpaceProcessingModeValues.Preserve);
    }

    [Fact]
    public void Build_EmptyCode_ReturnsTableWithEmptyCell()
    {
        var builder = new CodeBlockBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build("", null, _theme);

        var cell = table.Descendants<TableCell>().First();
        cell.ShouldNotBeNull();
    }

    [Fact]
    public void Build_WithLanguage_StoresLanguageInfo()
    {
        var builder = new CodeBlockBuilder(new ParagraphBuilder(_theme));
        var code = "var x = 1;";

        // Language is passed but not used for highlighting yet (Issue #21)
        // Should not throw
        var table = builder.Build(code, "csharp", _theme);

        table.ShouldNotBeNull();
    }

    [Fact]
    public void Build_HasReducedFontSize()
    {
        var builder = new CodeBlockBuilder(new ParagraphBuilder(_theme));
        var code = "hello";

        var table = builder.Build(code, null, _theme);

        var run = table.Descendants<Run>().First();
        var fontSize = run.RunProperties?.FontSize;
        fontSize.ShouldNotBeNull();
        // Code font should be smaller than body font
        var codeHalfPoints = int.Parse(fontSize!.Val!.Value!);
        var bodyHalfPoints = (int)(_theme.BaseFontSize * 2);
        codeHalfPoints.ShouldBeLessThanOrEqualTo(bodyHalfPoints);
    }
}
