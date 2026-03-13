// agent-notes: { ctx: "Tests for CodeBlockBuilder: mono font, background, borders, contrast", deps: [Md2.Emit.Docx.CodeBlockBuilder, DocumentFormat.OpenXml], state: active, last: "tara@2026-03-13" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Ast;
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

    // -----------------------------------------------------------------------
    // Contrast handling (issue #90)
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_DarkBackground_DarkTokenColor_AdjustsForContrast()
    {
        // hackterm-like theme: dark background, token color also dark → should adjust
        var darkTheme = new ResolvedTheme
        {
            CodeBackgroundColor = "0D1117",  // near-black
            BodyTextColor = "C9D1D9",        // light grey
        };
        var tokens = new List<SyntaxToken>
        {
            new("keyword", "1F2328", SyntaxFontStyle.Normal),  // very dark blue → invisible on dark bg
        };
        var builder = new CodeBlockBuilder(new ParagraphBuilder(darkTheme));

        var table = builder.Build("keyword", "csharp", darkTheme, tokens);

        var run = table.Descendants<Run>().First();
        var colorVal = run.RunProperties?.Color?.Val?.Value;
        colorVal.ShouldNotBeNull();
        // The adjusted color should NOT be the original dark color
        colorVal.ShouldNotBe("1F2328");
        // It should be a light color (high luminance)
        var r = Convert.ToInt32(colorVal![..2], 16);
        var g = Convert.ToInt32(colorVal[2..4], 16);
        var b = Convert.ToInt32(colorVal[4..6], 16);
        var avg = (r + g + b) / 3.0;
        avg.ShouldBeGreaterThan(120, "Token on dark background should be adjusted to a light color");
    }

    [Fact]
    public void Build_LightBackground_LightTokenColor_AdjustsForContrast()
    {
        // Light background, token color also light → should adjust
        var lightTheme = new ResolvedTheme
        {
            CodeBackgroundColor = "FFFFFF",
            BodyTextColor = "333333",
        };
        var tokens = new List<SyntaxToken>
        {
            new("text", "F0F0F0", SyntaxFontStyle.Normal),  // very light → invisible on white bg
        };
        var builder = new CodeBlockBuilder(new ParagraphBuilder(lightTheme));

        var table = builder.Build("text", "csharp", lightTheme, tokens);

        var run = table.Descendants<Run>().First();
        var colorVal = run.RunProperties?.Color?.Val?.Value;
        colorVal.ShouldNotBeNull();
        colorVal.ShouldNotBe("F0F0F0");
        var r = Convert.ToInt32(colorVal![..2], 16);
        var g = Convert.ToInt32(colorVal[2..4], 16);
        var b = Convert.ToInt32(colorVal[4..6], 16);
        var avg = (r + g + b) / 3.0;
        avg.ShouldBeLessThan(120, "Token on light background should be adjusted to a dark color");
    }

    [Fact]
    public void Build_DarkBackground_LightTokenColor_PreservesColor()
    {
        // Good contrast: dark bg + light token → no adjustment needed
        var darkTheme = new ResolvedTheme
        {
            CodeBackgroundColor = "0D1117",
            BodyTextColor = "C9D1D9",
        };
        var tokens = new List<SyntaxToken>
        {
            new("bright", "FF7B72", SyntaxFontStyle.Normal),  // bright red on dark bg = good contrast
        };
        var builder = new CodeBlockBuilder(new ParagraphBuilder(darkTheme));

        var table = builder.Build("bright", "csharp", darkTheme, tokens);

        var run = table.Descendants<Run>().First();
        var colorVal = run.RunProperties?.Color?.Val?.Value;
        colorVal.ShouldBe("FF7B72");
    }

    [Fact]
    public void Build_DefaultTheme_TokenColors_Unchanged()
    {
        // Default theme (light bg) with typical dark token colors → should not change
        var tokens = new List<SyntaxToken>
        {
            new("var", "0000FF", SyntaxFontStyle.Normal),  // dark blue on light bg = fine
        };
        var builder = new CodeBlockBuilder(new ParagraphBuilder(_theme));

        var table = builder.Build("var", "csharp", _theme, tokens);

        var run = table.Descendants<Run>().First();
        run.RunProperties?.Color?.Val?.Value.ShouldBe("0000FF");
    }
}
