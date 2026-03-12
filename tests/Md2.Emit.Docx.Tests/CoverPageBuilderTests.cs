// agent-notes: { ctx: "Tests for CoverPageBuilder: title, subtitle, author, date, abstract", deps: [Md2.Emit.Docx.CoverPageBuilder, DocumentMetadata], state: active, last: "sato@2026-03-12" }

using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Ast;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class CoverPageBuilderTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();

    [Fact]
    public void Build_NullMetadata_ReturnsEmpty()
    {
        var builder = new CoverPageBuilder(new ParagraphBuilder(_theme));
        var elements = builder.Build(null, _theme);
        elements.Count.ShouldBe(0);
    }

    [Fact]
    public void Build_NoTitle_ReturnsEmpty()
    {
        var builder = new CoverPageBuilder(new ParagraphBuilder(_theme));
        var metadata = new DocumentMetadata { Author = "John" };
        var elements = builder.Build(metadata, _theme);
        elements.Count.ShouldBe(0);
    }

    [Fact]
    public void Build_WithTitle_ReturnsCoverElements()
    {
        var builder = new CoverPageBuilder(new ParagraphBuilder(_theme));
        var metadata = new DocumentMetadata { Title = "Test Document" };
        var elements = builder.Build(metadata, _theme);
        elements.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Build_WithTitle_ContainsTitleText()
    {
        var builder = new CoverPageBuilder(new ParagraphBuilder(_theme));
        var metadata = new DocumentMetadata { Title = "My Report" };
        var elements = builder.Build(metadata, _theme);

        var allText = string.Join("", elements.SelectMany(e => e.Descendants<Text>()).Select(t => t.Text));
        allText.ShouldContain("My Report");
    }

    [Fact]
    public void Build_TitleIsCentered()
    {
        var builder = new CoverPageBuilder(new ParagraphBuilder(_theme));
        var metadata = new DocumentMetadata { Title = "Centered Title" };
        var elements = builder.Build(metadata, _theme);

        // Find the paragraph containing the title text
        var titlePara = elements.OfType<Paragraph>()
            .First(p => p.Descendants<Text>().Any(t => t.Text == "Centered Title"));
        var justification = titlePara.ParagraphProperties?.GetFirstChild<Justification>();
        justification.ShouldNotBeNull();
        justification.Val!.Value.ShouldBe(JustificationValues.Center);
    }

    [Fact]
    public void Build_TitleIsBold()
    {
        var builder = new CoverPageBuilder(new ParagraphBuilder(_theme));
        var metadata = new DocumentMetadata { Title = "Bold Title" };
        var elements = builder.Build(metadata, _theme);

        var titlePara = elements.OfType<Paragraph>()
            .First(p => p.Descendants<Text>().Any(t => t.Text == "Bold Title"));
        var bold = titlePara.Descendants<Run>().First().RunProperties?.GetFirstChild<Bold>();
        bold.ShouldNotBeNull();
    }

    [Fact]
    public void Build_WithSubject_ContainsSubtitleText()
    {
        var builder = new CoverPageBuilder(new ParagraphBuilder(_theme));
        var metadata = new DocumentMetadata { Title = "Title", Subject = "A Subtitle" };
        var elements = builder.Build(metadata, _theme);

        var allText = string.Join("", elements.SelectMany(e => e.Descendants<Text>()).Select(t => t.Text));
        allText.ShouldContain("A Subtitle");
    }

    [Fact]
    public void Build_WithAuthor_ContainsAuthorText()
    {
        var builder = new CoverPageBuilder(new ParagraphBuilder(_theme));
        var metadata = new DocumentMetadata { Title = "Title", Author = "Jane Doe" };
        var elements = builder.Build(metadata, _theme);

        var allText = string.Join("", elements.SelectMany(e => e.Descendants<Text>()).Select(t => t.Text));
        allText.ShouldContain("Jane Doe");
    }

    [Fact]
    public void Build_WithDate_ContainsDateText()
    {
        var builder = new CoverPageBuilder(new ParagraphBuilder(_theme));
        var metadata = new DocumentMetadata { Title = "Title", Date = "2026-03-12" };
        var elements = builder.Build(metadata, _theme);

        var allText = string.Join("", elements.SelectMany(e => e.Descendants<Text>()).Select(t => t.Text));
        allText.ShouldContain("2026-03-12");
    }

    [Fact]
    public void Build_WithAbstract_ContainsAbstractText()
    {
        var builder = new CoverPageBuilder(new ParagraphBuilder(_theme));
        var metadata = new DocumentMetadata
        {
            Title = "Title",
            CustomFields = new Dictionary<string, string> { ["abstract"] = "This is the abstract." }
        };
        var elements = builder.Build(metadata, _theme);

        var allText = string.Join("", elements.SelectMany(e => e.Descendants<Text>()).Select(t => t.Text));
        allText.ShouldContain("This is the abstract.");
    }

    [Fact]
    public void Build_EndsWithSectionBreak()
    {
        var builder = new CoverPageBuilder(new ParagraphBuilder(_theme));
        var metadata = new DocumentMetadata { Title = "Title" };
        var elements = builder.Build(metadata, _theme);

        var lastParagraph = elements.Last().ShouldBeOfType<Paragraph>();
        var sectPr = lastParagraph.ParagraphProperties?.GetFirstChild<SectionProperties>();
        sectPr.ShouldNotBeNull();
        var sectType = sectPr.GetFirstChild<SectionType>();
        sectType.ShouldNotBeNull();
        sectType.Val!.Value.ShouldBe(SectionMarkValues.NextPage);
    }
}
