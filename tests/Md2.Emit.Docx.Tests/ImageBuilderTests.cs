// agent-notes: { ctx: "Tests for ImageBuilder: placeholder, scaling, alt text", deps: [Md2.Emit.Docx.ImageBuilder, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

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
}
