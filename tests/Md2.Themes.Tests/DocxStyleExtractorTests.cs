// agent-notes: { ctx: "Tests for DocxStyleExtractor: fonts, sizes, colors, page layout", deps: [src/Md2.Themes/DocxStyleExtractor.cs, src/Md2.Themes/ThemeDefinition.cs], state: active, last: "sato@2026-03-12" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Shouldly;
using Md2.Themes;

namespace Md2.Themes.Tests;

public class DocxStyleExtractorTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { }
        }
    }

    // --- Normal/body style extraction ---

    [Fact]
    public void Extract_DocumentWithNormalStyle_ExtractsBodyFont()
    {
        var path = CreateTestDocx(styles =>
        {
            AddStyle(styles, "Normal", StyleValues.Paragraph, runProps: new RunProperties(
                new RunFonts { Ascii = "Georgia" }
            ));
        });

        var theme = DocxStyleExtractor.Extract(path);

        theme.Typography.ShouldNotBeNull();
        theme.Typography!.BodyFont.ShouldBe("Georgia");
    }

    [Fact]
    public void Extract_DocumentWithNormalStyle_ExtractsBaseFontSize()
    {
        // 24 half-points = 12pt
        var path = CreateTestDocx(styles =>
        {
            AddStyle(styles, "Normal", StyleValues.Paragraph, runProps: new RunProperties(
                new FontSize { Val = "24" }
            ));
        });

        var theme = DocxStyleExtractor.Extract(path);

        theme.Docx.ShouldNotBeNull();
        theme.Docx!.BaseFontSize.ShouldBe(12.0);
    }

    [Fact]
    public void Extract_DocumentWithNormalStyle_ExtractsBodyTextColor()
    {
        var path = CreateTestDocx(styles =>
        {
            AddStyle(styles, "Normal", StyleValues.Paragraph, runProps: new RunProperties(
                new Color { Val = "333333" }
            ));
        });

        var theme = DocxStyleExtractor.Extract(path);

        theme.Colors.ShouldNotBeNull();
        theme.Colors!.BodyText.ShouldBe("333333");
    }

    // --- Heading style extraction ---

    [Fact]
    public void Extract_DocumentWithHeading1Style_ExtractsHeadingFont()
    {
        var path = CreateTestDocx(styles =>
        {
            AddStyle(styles, "Heading1", StyleValues.Paragraph, runProps: new RunProperties(
                new RunFonts { Ascii = "Arial" }
            ));
        });

        var theme = DocxStyleExtractor.Extract(path);

        theme.Typography.ShouldNotBeNull();
        theme.Typography!.HeadingFont.ShouldBe("Arial");
    }

    [Fact]
    public void Extract_DocumentWithHeading1Style_ExtractsHeading1Size()
    {
        // 60 half-points = 30pt
        var path = CreateTestDocx(styles =>
        {
            AddStyle(styles, "Heading1", StyleValues.Paragraph, runProps: new RunProperties(
                new FontSize { Val = "60" }
            ));
        });

        var theme = DocxStyleExtractor.Extract(path);

        theme.Docx.ShouldNotBeNull();
        theme.Docx!.Heading1Size.ShouldBe(30.0);
    }

    [Fact]
    public void Extract_DocumentWithHeading2Style_ExtractsHeading2Size()
    {
        // 48 half-points = 24pt
        var path = CreateTestDocx(styles =>
        {
            AddStyle(styles, "Heading2", StyleValues.Paragraph, runProps: new RunProperties(
                new FontSize { Val = "48" }
            ));
        });

        var theme = DocxStyleExtractor.Extract(path);

        theme.Docx.ShouldNotBeNull();
        theme.Docx!.Heading2Size.ShouldBe(24.0);
    }

    // --- Page layout extraction ---

    [Fact]
    public void Extract_DocumentWithPageLayout_ExtractsPageDimensions()
    {
        var path = CreateTestDocx(
            configureStyles: null,
            configureSectPr: sectPr =>
            {
                sectPr.Append(new PageSize
                {
                    Width = 12240,
                    Height = 15840
                });
            });

        var theme = DocxStyleExtractor.Extract(path);

        theme.Docx.ShouldNotBeNull();
        theme.Docx!.Page.ShouldNotBeNull();
        theme.Docx.Page!.Width.ShouldBe(12240u);
        theme.Docx.Page.Height.ShouldBe(15840u);
    }

    [Fact]
    public void Extract_DocumentWithPageMargins_ExtractsMargins()
    {
        var path = CreateTestDocx(
            configureStyles: null,
            configureSectPr: sectPr =>
            {
                sectPr.Append(new PageMargin
                {
                    Top = 1440,
                    Bottom = 1440,
                    Left = 1800u,
                    Right = 1800u
                });
            });

        var theme = DocxStyleExtractor.Extract(path);

        theme.Docx.ShouldNotBeNull();
        theme.Docx!.Page.ShouldNotBeNull();
        theme.Docx.Page!.MarginTop.ShouldBe(1440);
        theme.Docx.Page.MarginBottom.ShouldBe(1440);
        theme.Docx.Page.MarginLeft.ShouldBe(1800);
        theme.Docx.Page.MarginRight.ShouldBe(1800);
    }

    // --- Hyperlink style extraction ---

    [Fact]
    public void Extract_DocumentWithHyperlinkStyle_ExtractsLinkColor()
    {
        var path = CreateTestDocx(styles =>
        {
            AddStyle(styles, "Hyperlink", StyleValues.Character, runProps: new RunProperties(
                new Color { Val = "4A90D9" }
            ));
        });

        var theme = DocxStyleExtractor.Extract(path);

        theme.Colors.ShouldNotBeNull();
        theme.Colors!.Link.ShouldBe("4A90D9");
    }

    // --- Edge cases ---

    [Fact]
    public void Extract_EmptyDocument_ReturnsEmptyThemeDefinition()
    {
        var path = CreateTestDocx(configureStyles: null);

        var theme = DocxStyleExtractor.Extract(path);

        theme.ShouldNotBeNull();
        theme.Typography.ShouldBeNull();
        theme.Colors.ShouldBeNull();
        // Docx section may exist if page layout is present, but individual values should be null
        // For a truly empty document, check that no font sizes are extracted
        if (theme.Docx is not null)
        {
            theme.Docx.BaseFontSize.ShouldBeNull();
            theme.Docx.Heading1Size.ShouldBeNull();
        }
    }

    [Fact]
    public void Extract_NonexistentFile_ThrowsFileNotFoundException()
    {
        Should.Throw<FileNotFoundException>(() =>
            DocxStyleExtractor.Extract("/nonexistent/path/template.docx"));
    }

    // --- Helper methods ---

    /// <summary>
    /// Creates a minimal DOCX file with specified styles and section properties.
    /// Returns the path to the temporary file.
    /// </summary>
    private string CreateTestDocx(
        Action<StyleDefinitionsPart>? configureStyles,
        Action<SectionProperties>? configureSectPr = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.docx");
        _tempFiles.Add(path);

        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        if (configureStyles is not null)
        {
            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles();
            configureStyles(stylesPart);
            stylesPart.Styles.Save();
        }

        if (configureSectPr is not null)
        {
            var sectPr = new SectionProperties();
            configureSectPr(sectPr);
            mainPart.Document.Body!.Append(sectPr);
        }

        mainPart.Document.Save();

        return path;
    }

    /// <summary>
    /// Adds a style definition to the styles part with optional run properties.
    /// </summary>
    private static void AddStyle(
        StyleDefinitionsPart stylesPart,
        string styleId,
        StyleValues styleType,
        RunProperties? runProps = null)
    {
        var style = new Style
        {
            Type = styleType,
            StyleId = styleId
        };
        style.Append(new StyleName { Val = styleId });

        if (runProps is not null)
        {
            style.Append(new StyleRunProperties());
            // Copy children from the provided RunProperties into StyleRunProperties
            var styleRunProps = style.GetFirstChild<StyleRunProperties>()!;
            foreach (var child in runProps.ChildElements.ToList())
            {
                styleRunProps.Append(child.CloneNode(true));
            }
        }

        stylesPart.Styles!.Append(style);
    }
}
