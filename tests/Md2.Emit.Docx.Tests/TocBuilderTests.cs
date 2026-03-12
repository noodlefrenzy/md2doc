// agent-notes: { ctx: "Tests for TocBuilder: field code, depth, styling", deps: [Md2.Emit.Docx.TocBuilder, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-12" }

using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class TocBuilderTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();

    [Fact]
    public void Build_ReturnsThreeElements()
    {
        var builder = new TocBuilder(new ParagraphBuilder(_theme));
        var elements = builder.Build(3, _theme);

        // Heading + TOC field paragraph + page break paragraph
        elements.Count.ShouldBe(3);
    }

    [Fact]
    public void Build_FirstElementIsHeadingParagraph()
    {
        var builder = new TocBuilder(new ParagraphBuilder(_theme));
        var elements = builder.Build(3, _theme);

        var heading = elements[0].ShouldBeOfType<Paragraph>();
        var text = string.Join("", heading.Descendants<Text>().Select(t => t.Text));
        text.ShouldBe("Table of Contents");
    }

    [Fact]
    public void Build_SecondElementContainsTocFieldCode()
    {
        var builder = new TocBuilder(new ParagraphBuilder(_theme));
        var elements = builder.Build(3, _theme);

        var tocParagraph = elements[1].ShouldBeOfType<Paragraph>();
        var fieldCodes = tocParagraph.Descendants<FieldCode>().ToList();
        fieldCodes.Count.ShouldBe(1);
        fieldCodes[0].InnerText.ShouldContain("TOC");
        fieldCodes[0].InnerText.ShouldContain("1-3");
    }

    [Theory]
    [InlineData(1, "1-1")]
    [InlineData(2, "1-2")]
    [InlineData(4, "1-4")]
    [InlineData(6, "1-6")]
    public void Build_FieldCodeReflectsDepth(int depth, string expectedRange)
    {
        var builder = new TocBuilder(new ParagraphBuilder(_theme));
        var elements = builder.Build(depth, _theme);

        var tocParagraph = (Paragraph)elements[1];
        var fieldCode = tocParagraph.Descendants<FieldCode>().First();
        fieldCode.InnerText.ShouldContain(expectedRange);
    }

    [Fact]
    public void Build_DepthClamped_Below1()
    {
        var builder = new TocBuilder(new ParagraphBuilder(_theme));
        var elements = builder.Build(0, _theme);

        var fieldCode = ((Paragraph)elements[1]).Descendants<FieldCode>().First();
        fieldCode.InnerText.ShouldContain("1-1");
    }

    [Fact]
    public void Build_DepthClamped_Above6()
    {
        var builder = new TocBuilder(new ParagraphBuilder(_theme));
        var elements = builder.Build(10, _theme);

        var fieldCode = ((Paragraph)elements[1]).Descendants<FieldCode>().First();
        fieldCode.InnerText.ShouldContain("1-6");
    }

    [Fact]
    public void Build_ThirdElementContainsPageBreak()
    {
        var builder = new TocBuilder(new ParagraphBuilder(_theme));
        var elements = builder.Build(3, _theme);

        var breakParagraph = elements[2].ShouldBeOfType<Paragraph>();
        var breaks = breakParagraph.Descendants<Break>().ToList();
        breaks.Count.ShouldBe(1);
        breaks[0].Type!.Value.ShouldBe(BreakValues.Page);
    }

    [Fact]
    public void Build_FieldHasHyperlinkSwitch()
    {
        var builder = new TocBuilder(new ParagraphBuilder(_theme));
        var elements = builder.Build(3, _theme);

        var fieldCode = ((Paragraph)elements[1]).Descendants<FieldCode>().First();
        fieldCode.InnerText.ShouldContain("\\h");
    }

    [Fact]
    public void Build_TocFieldHasBeginSeparateEnd()
    {
        var builder = new TocBuilder(new ParagraphBuilder(_theme));
        var elements = builder.Build(3, _theme);

        var tocParagraph = (Paragraph)elements[1];
        var fieldChars = tocParagraph.Descendants<FieldChar>().ToList();
        fieldChars.Count.ShouldBe(3);
        fieldChars[0].FieldCharType!.Value.ShouldBe(FieldCharValues.Begin);
        fieldChars[1].FieldCharType!.Value.ShouldBe(FieldCharValues.Separate);
        fieldChars[2].FieldCharType!.Value.ShouldBe(FieldCharValues.End);
    }
}
