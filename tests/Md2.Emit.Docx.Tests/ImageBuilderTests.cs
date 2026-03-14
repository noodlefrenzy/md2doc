// agent-notes: { ctx: "Tests for ImageBuilder: placeholder, scaling, alt text, captions, path safety", deps: [Md2.Emit.Docx.ImageBuilder, DocumentFormat.OpenXml], state: red, last: "tara@2026-03-14" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Pipeline;
using Md2.Emit.Docx;
using Shouldly;

namespace Md2.Emit.Docx.Tests;

public class ImageBuilderTests
{
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();

    [Fact]
    public void BuildPlaceholder_MissingFile_ReturnsParagraphWithWarningText()
    {
        var builder = new ImageBuilder(new ParagraphBuilder(_theme));
        var path = "nonexistent/image.png";

        var paragraph = builder.BuildPlaceholder(path);

        paragraph.ShouldNotBeNull();
        paragraph.ShouldBeOfType<Paragraph>();
        var text = string.Join("", paragraph.Descendants<Text>().Select(t => t.Text));
        text.ShouldContain("[Image not found: nonexistent/image.png]");
    }

    [Fact]
    public void CalculateScaledDimensions_WidthExceedsMax_ScalesByWidth()
    {
        // Original 1000x500, max width 800 => 800x400
        var (width, height) = ImageBuilder.CalculateScaledDimensions(1000, 500, 800, 10000);

        width.ShouldBe(800);
        height.ShouldBe(400);
    }

    [Fact]
    public void CalculateScaledDimensions_HeightExceedsMax_ScalesByHeight()
    {
        // Original 500x1000, max width 800, max height 600 => 300x600
        var (width, height) = ImageBuilder.CalculateScaledDimensions(500, 1000, 800, 600);

        width.ShouldBe(300);
        height.ShouldBe(600);
    }

    [Fact]
    public void CalculateScaledDimensions_FitsWithinBounds_NoScaling()
    {
        // Original 400x300, max width 800, max height 600 => 400x300
        var (width, height) = ImageBuilder.CalculateScaledDimensions(400, 300, 800, 600);

        width.ShouldBe(400);
        height.ShouldBe(300);
    }

    [Fact]
    public void CalculateScaledDimensions_PreservesAspectRatio()
    {
        // Original 1920x1080 (16:9), max width 800
        var (width, height) = ImageBuilder.CalculateScaledDimensions(1920, 1080, 800, 10000);

        width.ShouldBe(800);
        // 1080 * (800/1920) = 450
        height.ShouldBe(450);
    }

    [Fact]
    public void CalculateScaledDimensions_BothExceed_ScalesByLargerFactor()
    {
        // Original 1600x1200, max width 800, max height 400
        // Scale by width: 800/1600 = 0.5 => 800x600, height still exceeds 400
        // Scale by height: 400/1200 = 0.333 => 533x400
        var (width, height) = ImageBuilder.CalculateScaledDimensions(1600, 1200, 800, 400);

        width.ShouldBe(533);
        height.ShouldBe(400);
    }

    // -----------------------------------------------------------------------
    // Caption tests (RED phase)
    //
    // These tests target the upcoming signature change:
    //   BuildImage returns IReadOnlyList<Paragraph> instead of Paragraph.
    //
    // Until the implementation changes, these tests will NOT COMPILE.
    // That is intentional — this is TDD red phase. The implementer must:
    //   1. Change BuildImage return type to IReadOnlyList<Paragraph>
    //   2. Generate a caption paragraph when altText is non-empty
    //   3. Style the caption: centered, italic, font 2pt smaller than body
    //   4. Return only the image paragraph when altText is null/empty
    //
    // After the signature change, these tests should compile and FAIL
    // because the caption logic is not yet implemented.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a minimal valid 1x1 PNG file at the given path.
    /// </summary>
    private static string CreateMinimalPng()
    {
        // Minimal 1x1 white PNG (67 bytes)
        byte[] pngBytes =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, // IHDR chunk length
            0x49, 0x48, 0x44, 0x52, // IHDR
            0x00, 0x00, 0x00, 0x01, // width = 1
            0x00, 0x00, 0x00, 0x01, // height = 1
            0x08, 0x02,             // 8-bit RGB
            0x00, 0x00, 0x00,       // compression, filter, interlace
            0x90, 0x77, 0x53, 0xDE, // IHDR CRC
            0x00, 0x00, 0x00, 0x0C, // IDAT chunk length
            0x49, 0x44, 0x41, 0x54, // IDAT
            0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00, 0x00,
            0x00, 0x02, 0x00, 0x01, // compressed data
            0xE2, 0x21, 0xBC, 0x33, // IDAT CRC
            0x00, 0x00, 0x00, 0x00, // IEND chunk length
            0x49, 0x45, 0x4E, 0x44, // IEND
            0xAE, 0x42, 0x60, 0x82  // IEND CRC
        ];

        var path = Path.Combine(Path.GetTempPath(), $"md2_test_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, pngBytes);
        return path;
    }

    /// <summary>
    /// Creates a WordprocessingDocument in memory and returns its MainDocumentPart.
    /// The caller must dispose the doc before the stream (inner-first ordering).
    /// </summary>
    private static (WordprocessingDocument doc, MemoryStream stream) CreateInMemoryDocument()
    {
        var stream = new MemoryStream();
        var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, false);
        doc.AddMainDocumentPart();
        doc.MainDocumentPart!.Document = new Document(new Body());
        return (doc, stream);
    }

    [Fact]
    public void BuildImage_WithAltText_ReturnsTwoParagraphs()
    {
        var pngPath = CreateMinimalPng();
        try
        {
            var (doc, stream) = CreateInMemoryDocument();
            using (stream)
            using (doc)
            {
                var builder = new ImageBuilder(new ParagraphBuilder(_theme));

                // After signature change: returns IReadOnlyList<Paragraph>
                var result = builder.BuildImage(doc.MainDocumentPart!, pngPath, "A test caption", _theme);

                result.Count.ShouldBe(2);
            }
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void BuildImage_WithAltText_SecondParagraphContainsCaptionText()
    {
        var pngPath = CreateMinimalPng();
        try
        {
            var (doc, stream) = CreateInMemoryDocument();
            using (stream)
            using (doc)
            {
                var builder = new ImageBuilder(new ParagraphBuilder(_theme));
                var altText = "Figure 1: Architecture diagram";

                var result = builder.BuildImage(doc.MainDocumentPart!, pngPath, altText, _theme);

                var captionParagraph = result[1];
                var captionText = string.Join("", captionParagraph.Descendants<Text>().Select(t => t.Text));
                captionText.ShouldBe(altText);
            }
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void BuildImage_WithAltText_CaptionIsItalic()
    {
        var pngPath = CreateMinimalPng();
        try
        {
            var (doc, stream) = CreateInMemoryDocument();
            using (stream)
            using (doc)
            {
                var builder = new ImageBuilder(new ParagraphBuilder(_theme));

                var result = builder.BuildImage(doc.MainDocumentPart!, pngPath, "Italic caption", _theme);

                var captionParagraph = result[1];
                var runProps = captionParagraph.Descendants<Run>().First().RunProperties;
                runProps.ShouldNotBeNull();
                runProps.GetFirstChild<Italic>().ShouldNotBeNull();
            }
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void BuildImage_WithAltText_CaptionIsCentered()
    {
        var pngPath = CreateMinimalPng();
        try
        {
            var (doc, stream) = CreateInMemoryDocument();
            using (stream)
            using (doc)
            {
                var builder = new ImageBuilder(new ParagraphBuilder(_theme));

                var result = builder.BuildImage(doc.MainDocumentPart!, pngPath, "Centered caption", _theme);

                var captionParagraph = result[1];
                var paraProps = captionParagraph.ParagraphProperties;
                paraProps.ShouldNotBeNull();
                var justification = paraProps.GetFirstChild<Justification>();
                justification.ShouldNotBeNull();
                justification.Val!.Value.ShouldBe(JustificationValues.Center);
            }
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void BuildImage_WithAltText_CaptionHasSmallerFont()
    {
        var pngPath = CreateMinimalPng();
        try
        {
            var (doc, stream) = CreateInMemoryDocument();
            using (stream)
            using (doc)
            {
                var builder = new ImageBuilder(new ParagraphBuilder(_theme));

                var result = builder.BuildImage(doc.MainDocumentPart!, pngPath, "Small font caption", _theme);

                var captionParagraph = result[1];
                var fontSize = captionParagraph.Descendants<Run>().First()
                    .RunProperties?.GetFirstChild<FontSize>();
                fontSize.ShouldNotBeNull();

                // Caption should be 2pt smaller than body.
                // BaseFontSize is 11pt => caption is 9pt => half-points = 18
                var expectedHalfPoints = ((int)((_theme.BaseFontSize - 2) * 2)).ToString();
                fontSize.Val!.Value.ShouldBe(expectedHalfPoints);
            }
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void BuildImage_WithEmptyAltText_ReturnsSingleParagraph()
    {
        var pngPath = CreateMinimalPng();
        try
        {
            var (doc, stream) = CreateInMemoryDocument();
            using (stream)
            using (doc)
            {
                var builder = new ImageBuilder(new ParagraphBuilder(_theme));

                var result = builder.BuildImage(doc.MainDocumentPart!, pngPath, "", _theme);

                result.Count.ShouldBe(1);
            }
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void BuildImage_WithNullAltText_ReturnsSingleParagraph()
    {
        var pngPath = CreateMinimalPng();
        try
        {
            var (doc, stream) = CreateInMemoryDocument();
            using (stream)
            using (doc)
            {
                var builder = new ImageBuilder(new ParagraphBuilder(_theme));

                // Note: this may require changing the altText parameter to string? (nullable)
                var result = builder.BuildImage(doc.MainDocumentPart!, pngPath, null!, _theme);

                result.Count.ShouldBe(1);
            }
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    // ── H-2: Path traversal prevention ──────────────────────────────────

    [Fact]
    public void IsPathSafe_AbsolutePath_ReturnsFalse()
    {
        ImageBuilder.IsPathSafe("/etc/hostname", "/home/user/docs").ShouldBeFalse();
    }

    [Fact]
    public void IsPathSafe_PathTraversal_ReturnsFalse()
    {
        ImageBuilder.IsPathSafe("../../../etc/hostname", "/home/user/docs").ShouldBeFalse();
    }

    [Fact]
    public void IsPathSafe_RelativeWithinBaseDirectory_ReturnsTrue()
    {
        ImageBuilder.IsPathSafe("images/photo.png", "/home/user/docs").ShouldBeTrue();
    }

    [Fact]
    public void IsPathSafe_SimpleFilename_ReturnsTrue()
    {
        ImageBuilder.IsPathSafe("photo.png", "/home/user/docs").ShouldBeTrue();
    }

    [Fact]
    public void IsPathSafe_NullPath_ReturnsFalse()
    {
        ImageBuilder.IsPathSafe(null!, null).ShouldBeFalse();
    }

    [Fact(Skip = "Windows-only: drive-letter paths are not rooted on Linux")]
    public void IsPathSafe_WindowsAbsolutePath_ReturnsFalse()
    {
        ImageBuilder.IsPathSafe(@"C:\Windows\System32\config", @"C:\Users\user\docs").ShouldBeFalse();
    }

    [Fact]
    public void IsPathSafe_HiddenTraversalWithDotSegment_ReturnsFalse()
    {
        // Path that resolves outside the base even though it starts relative
        ImageBuilder.IsPathSafe("images/../../etc/passwd", "/home/user/docs").ShouldBeFalse();
    }
}
