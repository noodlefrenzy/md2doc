// agent-notes: { ctx: "Tests for DocxEmitter, visitor, inline formatting", deps: [Md2.Emit.Docx, Md2.Core, DocumentFormat.OpenXml], state: active, last: "tara@2026-03-12" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Md2.Parsing;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class DocxEmitterTests
{
    private static MarkdownDocument ParseMarkdown(string markdown)
    {
        var pipeline = Md2MarkdownPipeline.Build(new ParserOptions());
        return Markdown.Parse(markdown, pipeline);
    }

    private static async Task<WordprocessingDocument> EmitToDocument(string markdown)
    {
        var doc = ParseMarkdown(markdown);
        var theme = ResolvedTheme.CreateDefault();
        var options = new EmitOptions();
        var emitter = new DocxEmitter();
        var stream = new MemoryStream();

        await emitter.EmitAsync(doc, theme, options, stream);
        stream.Position = 0;

        return WordprocessingDocument.Open(stream, false);
    }

    // ── IFormatEmitter contract ─────────────────────────────────────

    [Fact]
    public void FormatName_ReturnsDocx()
    {
        var emitter = new DocxEmitter();
        emitter.FormatName.ShouldBe("docx");
    }

    [Fact]
    public void FileExtensions_ContainsDocx()
    {
        var emitter = new DocxEmitter();
        emitter.FileExtensions.ShouldContain(".docx");
    }

    // ── Document structure ──────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_CreatesValidDocument()
    {
        using var wordDoc = await EmitToDocument("# Hello");

        wordDoc.ShouldNotBeNull();
        wordDoc.MainDocumentPart.ShouldNotBeNull();
        wordDoc.MainDocumentPart!.Document.ShouldNotBeNull();
        wordDoc.MainDocumentPart.Document.Body.ShouldNotBeNull();
    }

    [Fact]
    public async Task EmitAsync_EmptyDocument_ProducesValidDocx()
    {
        using var wordDoc = await EmitToDocument("");

        wordDoc.MainDocumentPart.ShouldNotBeNull();
        wordDoc.MainDocumentPart!.Document.Body.ShouldNotBeNull();
    }

    // ── Heading paragraphs (Issue 8) ────────────────────────────────

    [Theory]
    [InlineData("# Heading 1", "Heading1")]
    [InlineData("## Heading 2", "Heading2")]
    [InlineData("### Heading 3", "Heading3")]
    [InlineData("#### Heading 4", "Heading4")]
    [InlineData("##### Heading 5", "Heading5")]
    [InlineData("###### Heading 6", "Heading6")]
    public async Task EmitAsync_Heading_UsesCorrectStyleId(string markdown, string expectedStyleId)
    {
        using var wordDoc = await EmitToDocument(markdown);

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var paragraphs = body.Elements<Paragraph>().ToList();

        paragraphs.ShouldNotBeEmpty();
        var headingParagraph = paragraphs.First();
        var styleId = headingParagraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        styleId.ShouldBe(expectedStyleId);
    }

    [Fact]
    public async Task EmitAsync_Heading_ContainsCorrectText()
    {
        using var wordDoc = await EmitToDocument("# My Title");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var paragraph = body.Elements<Paragraph>().First();
        var text = string.Join("", paragraph.Descendants<Text>().Select(t => t.Text));
        text.ShouldBe("My Title");
    }

    // ── Body paragraphs (Issue 8) ───────────────────────────────────

    [Fact]
    public async Task EmitAsync_Paragraph_UsesNormalStyle()
    {
        using var wordDoc = await EmitToDocument("Just a paragraph.");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var paragraph = body.Elements<Paragraph>().First();
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        styleId.ShouldBe("Normal");
    }

    [Fact]
    public async Task EmitAsync_Paragraph_ContainsCorrectText()
    {
        using var wordDoc = await EmitToDocument("Hello world.");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var paragraph = body.Elements<Paragraph>().First();
        var text = string.Join("", paragraph.Descendants<Text>().Select(t => t.Text));
        text.ShouldBe("Hello world.");
    }

    // ── Widow/orphan control (Issue 8) ──────────────────────────────

    [Fact]
    public async Task EmitAsync_AllParagraphs_HaveWidowControl()
    {
        using var wordDoc = await EmitToDocument("# Heading\n\nParagraph text.");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var paragraphs = body.Elements<Paragraph>().ToList();

        foreach (var paragraph in paragraphs)
        {
            paragraph.ParagraphProperties.ShouldNotBeNull();
            paragraph.ParagraphProperties!.WidowControl.ShouldNotBeNull();
        }
    }

    // ── Bold (Issue 9) ──────────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_BoldText_HasBoldRunProperty()
    {
        using var wordDoc = await EmitToDocument("This is **bold** text.");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var runs = body.Descendants<Run>().ToList();
        var boldRun = runs.FirstOrDefault(r => r.RunProperties?.Bold != null);
        boldRun.ShouldNotBeNull();
        var text = string.Join("", boldRun!.Descendants<Text>().Select(t => t.Text));
        text.ShouldBe("bold");
    }

    // ── Italic (Issue 9) ────────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_ItalicText_HasItalicRunProperty()
    {
        using var wordDoc = await EmitToDocument("This is *italic* text.");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var runs = body.Descendants<Run>().ToList();
        var italicRun = runs.FirstOrDefault(r => r.RunProperties?.Italic != null);
        italicRun.ShouldNotBeNull();
        var text = string.Join("", italicRun!.Descendants<Text>().Select(t => t.Text));
        text.ShouldBe("italic");
    }

    // ── Strikethrough (Issue 9) ─────────────────────────────────────

    [Fact]
    public async Task EmitAsync_StrikethroughText_HasStrikeRunProperty()
    {
        using var wordDoc = await EmitToDocument("This is ~~struck~~ text.");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var runs = body.Descendants<Run>().ToList();
        var strikeRun = runs.FirstOrDefault(r => r.RunProperties?.Strike != null);
        strikeRun.ShouldNotBeNull();
        var text = string.Join("", strikeRun!.Descendants<Text>().Select(t => t.Text));
        text.ShouldBe("struck");
    }

    // ── Inline code (Issue 9) ───────────────────────────────────────

    [Fact]
    public async Task EmitAsync_InlineCode_HasMonoFontAndShading()
    {
        using var wordDoc = await EmitToDocument("Use `Console.WriteLine` here.");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var runs = body.Descendants<Run>().ToList();
        var codeRun = runs.FirstOrDefault(r => r.RunProperties?.Shading != null);
        codeRun.ShouldNotBeNull();

        var text = string.Join("", codeRun!.Descendants<Text>().Select(t => t.Text));
        text.ShouldBe("Console.WriteLine");

        codeRun.RunProperties!.Shading!.Fill!.Value.ShouldBe("F5F5F5");
    }

    // ── Hyperlinks (Issue 9) ────────────────────────────────────────

    [Fact]
    public async Task EmitAsync_Hyperlink_CreatesHyperlinkElement()
    {
        using var wordDoc = await EmitToDocument("Visit [Google](https://www.google.com).");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var hyperlinks = body.Descendants<Hyperlink>().ToList();
        hyperlinks.ShouldNotBeEmpty();

        var text = string.Join("", hyperlinks.First().Descendants<Text>().Select(t => t.Text));
        text.ShouldBe("Google");
    }

    [Fact]
    public async Task EmitAsync_Hyperlink_HasRelationship()
    {
        using var wordDoc = await EmitToDocument("Visit [Google](https://www.google.com).");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var hyperlink = body.Descendants<Hyperlink>().First();
        hyperlink.Id.ShouldNotBeNull();

        var rel = wordDoc.MainDocumentPart!.HyperlinkRelationships
            .FirstOrDefault(r => r.Id == hyperlink.Id!.Value);
        rel.ShouldNotBeNull();
        rel!.Uri.ToString().ShouldBe("https://www.google.com/");
    }

    // ── Line breaks (Issue 9) ───────────────────────────────────────

    [Fact]
    public async Task EmitAsync_LineBreak_CreatesBreakElement()
    {
        // Two trailing spaces create a hard line break in Markdown
        using var wordDoc = await EmitToDocument("Line one  \nLine two");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var breaks = body.Descendants<Break>().ToList();
        breaks.ShouldNotBeEmpty();
    }

    // ── Page layout (Issue 10) ──────────────────────────────────────

    [Fact]
    public async Task EmitAsync_PageSize_IsA4()
    {
        using var wordDoc = await EmitToDocument("# Test");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var sectionProps = body.Elements<SectionProperties>().FirstOrDefault();
        sectionProps.ShouldNotBeNull();

        var pageSize = sectionProps!.GetFirstChild<PageSize>();
        pageSize.ShouldNotBeNull();
        pageSize!.Width!.Value.ShouldBe(11906U);
        pageSize.Height!.Value.ShouldBe(16838U);
    }

    [Fact]
    public async Task EmitAsync_PageMargins_AreDefault()
    {
        using var wordDoc = await EmitToDocument("# Test");

        var body = wordDoc.MainDocumentPart!.Document.Body!;
        var sectionProps = body.Elements<SectionProperties>().First();
        var pageMargin = sectionProps.GetFirstChild<PageMargin>();
        pageMargin.ShouldNotBeNull();

        pageMargin!.Top!.Value.ShouldBe(1440);
        pageMargin.Bottom!.Value.ShouldBe(1440);
        pageMargin.Left!.Value.ShouldBe(1800U);
        pageMargin.Right!.Value.ShouldBe(1800U);
    }

    [Fact]
    public async Task EmitAsync_Footer_HasPageNumber()
    {
        using var wordDoc = await EmitToDocument("# Test");

        var mainPart = wordDoc.MainDocumentPart!;
        var footerParts = mainPart.FooterParts.ToList();
        footerParts.ShouldNotBeEmpty();

        var footer = footerParts.First().Footer;
        footer.ShouldNotBeNull();

        // Should contain a PAGE field code
        var fieldCodes = footer!.Descendants<FieldCode>().ToList();
        fieldCodes.ShouldNotBeEmpty();
        fieldCodes.Any(fc => fc.Text.Contains("PAGE")).ShouldBeTrue();
    }

    // ── Document properties (Issue 11) ──────────────────────────────

    [Fact]
    public async Task EmitAsync_FrontMatter_SetsDocumentProperties()
    {
        var markdown = "---\ntitle: My Document\nauthor: Test Author\n---\n\n# Hello";
        var doc = ParseMarkdown(markdown);

        // Extract front matter and set metadata on AST
        var metadata = Md2.Parsing.FrontMatterExtractor.Extract(doc);
        doc.SetDocumentMetadata(metadata);

        var theme = ResolvedTheme.CreateDefault();
        var options = new EmitOptions();
        var emitter = new DocxEmitter();
        var stream = new MemoryStream();

        await emitter.EmitAsync(doc, theme, options, stream);
        stream.Position = 0;

        using var wordDoc = WordprocessingDocument.Open(stream, false);
        var corePropsPart = wordDoc.CoreFilePropertiesPart;
        corePropsPart.ShouldNotBeNull();
    }

    // ── Subject and Keywords (Issue 56) ──────────────────────────────

    [Fact]
    public async Task EmitAsync_FrontMatter_SubjectInCoreProperties()
    {
        var markdown = "---\ntitle: Test\nsubject: Computer Science\n---\n\n# Hello";
        using var wordDoc = await EmitWithFrontMatter(markdown);

        var xml = ReadCorePropertiesXml(wordDoc);
        xml.ShouldContain("Computer Science");
    }

    [Fact]
    public async Task EmitAsync_FrontMatter_KeywordsInCoreProperties()
    {
        var markdown = "---\ntitle: Test\nkeywords: dotnet, markdown, docx\n---\n\n# Hello";
        using var wordDoc = await EmitWithFrontMatter(markdown);

        var xml = ReadCorePropertiesXml(wordDoc);
        xml.ShouldContain("dotnet, markdown, docx");
    }

    [Fact]
    public async Task EmitAsync_FrontMatter_OnlySubject_StillSetsProperties()
    {
        var markdown = "---\nsubject: Physics\n---\n\n# Hello";
        using var wordDoc = await EmitWithFrontMatter(markdown);

        var corePropsPart = wordDoc.CoreFilePropertiesPart;
        corePropsPart.ShouldNotBeNull();

        var xml = ReadCorePropertiesXml(wordDoc);
        xml.ShouldContain("Physics");
    }

    [Fact]
    public async Task EmitAsync_FrontMatter_OnlyKeywords_StillSetsProperties()
    {
        var markdown = "---\nkeywords: cli, tools\n---\n\n# Hello";
        using var wordDoc = await EmitWithFrontMatter(markdown);

        var corePropsPart = wordDoc.CoreFilePropertiesPart;
        corePropsPart.ShouldNotBeNull();

        var xml = ReadCorePropertiesXml(wordDoc);
        xml.ShouldContain("cli, tools");
    }

    [Fact]
    public async Task EmitAsync_FrontMatter_AllProperties_AllPresent()
    {
        var markdown = "---\ntitle: My Doc\nauthor: Jane\nsubject: Testing\nkeywords: test, qa\n---\n\n# Hello";
        using var wordDoc = await EmitWithFrontMatter(markdown);

        var xml = ReadCorePropertiesXml(wordDoc);
        xml.ShouldContain("My Doc");
        xml.ShouldContain("Jane");
        xml.ShouldContain("Testing");
        xml.ShouldContain("test, qa");
    }

    private static async Task<WordprocessingDocument> EmitWithFrontMatter(string markdown)
    {
        var doc = ParseMarkdown(markdown);
        var metadata = Md2.Parsing.FrontMatterExtractor.Extract(doc);
        doc.SetDocumentMetadata(metadata);

        var theme = ResolvedTheme.CreateDefault();
        var options = new EmitOptions();
        var emitter = new DocxEmitter();
        var stream = new MemoryStream();

        await emitter.EmitAsync(doc, theme, options, stream);
        stream.Position = 0;

        return WordprocessingDocument.Open(stream, false);
    }

    private static string ReadCorePropertiesXml(WordprocessingDocument wordDoc)
    {
        var corePropsPart = wordDoc.CoreFilePropertiesPart!;
        using var reader = new StreamReader(corePropsPart.GetStream());
        return reader.ReadToEnd();
    }

    // ── ResolvedTheme defaults (Issue 13) ───────────────────────────

    [Fact]
    public void ResolvedTheme_CreateDefault_HasExpectedFonts()
    {
        var theme = ResolvedTheme.CreateDefault();

        theme.HeadingFont.ShouldBe("Calibri");
        theme.BodyFont.ShouldBe("Cambria");
        theme.MonoFont.ShouldBe("Cascadia Code");
    }

    [Fact]
    public void ResolvedTheme_CreateDefault_HasExpectedColors()
    {
        var theme = ResolvedTheme.CreateDefault();

        theme.PrimaryColor.ShouldBe("1B3A5C");
        theme.SecondaryColor.ShouldBe("4A90D9");
        theme.BodyTextColor.ShouldBe("333333");
    }

    [Fact]
    public void ResolvedTheme_CreateDefault_HasExpectedSizes()
    {
        var theme = ResolvedTheme.CreateDefault();

        theme.BaseFontSize.ShouldBe(11.0);
        theme.Heading1Size.ShouldBe(28.0);
        theme.Heading2Size.ShouldBe(22.0);
        theme.Heading3Size.ShouldBe(16.0);
        theme.Heading4Size.ShouldBe(13.0);
        theme.Heading5Size.ShouldBe(11.0);
        theme.Heading6Size.ShouldBe(11.0);
    }

    [Fact]
    public void ResolvedTheme_GetHeadingSize_ReturnsCorrectSize()
    {
        var theme = ResolvedTheme.CreateDefault();

        theme.GetHeadingSize(1).ShouldBe(28.0);
        theme.GetHeadingSize(2).ShouldBe(22.0);
        theme.GetHeadingSize(3).ShouldBe(16.0);
        theme.GetHeadingSize(7).ShouldBe(11.0); // fallback to base
    }

    [Fact]
    public void ResolvedTheme_CreateDefault_HasExpectedPageLayout()
    {
        var theme = ResolvedTheme.CreateDefault();

        theme.PageWidth.ShouldBe(11906U);
        theme.PageHeight.ShouldBe(16838U);
        theme.MarginTop.ShouldBe(1440);
        theme.MarginBottom.ShouldBe(1440);
        theme.MarginLeft.ShouldBe(1800);
        theme.MarginRight.ShouldBe(1800);
    }

    [Fact]
    public void ResolvedTheme_LineSpacing_IsCorrect()
    {
        var theme = ResolvedTheme.CreateDefault();
        theme.LineSpacing.ShouldBe(1.15);
    }
}
