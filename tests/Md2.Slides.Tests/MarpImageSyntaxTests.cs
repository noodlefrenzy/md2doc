// agent-notes: { ctx: "Tests for MarpImageSyntax parser", deps: [Md2.Slides.MarpImageSyntax], state: active, last: "tara@2026-03-15" }

using Md2.Slides;
using Shouldly;

namespace Md2.Slides.Tests;

public class MarpImageSyntaxTests
{
    // ── Background images ───────────────────────────────────────────

    [Fact]
    public void Parse_BgKeyword_SetsIsBackground()
    {
        var result = MarpImageSyntax.Parse("bg");

        result.ShouldNotBeNull();
        result!.IsBackground.ShouldBeTrue();
    }

    [Fact]
    public void Parse_BgCover_SetsSizeMode()
    {
        var result = MarpImageSyntax.Parse("bg cover");

        result.ShouldNotBeNull();
        result!.IsBackground.ShouldBeTrue();
        result.BackgroundSize.ShouldBe("cover");
    }

    [Fact]
    public void Parse_BgContain_SetsSizeMode()
    {
        var result = MarpImageSyntax.Parse("bg contain");

        result.ShouldNotBeNull();
        result!.BackgroundSize.ShouldBe("contain");
    }

    [Fact]
    public void Parse_BgFitMode_SetsSizeMode()
    {
        var result = MarpImageSyntax.Parse("bg fit");

        result.ShouldNotBeNull();
        result!.BackgroundSize.ShouldBe("fit");
    }

    [Fact]
    public void Parse_BgLeftSplit_SetsSplitDirection()
    {
        var result = MarpImageSyntax.Parse("bg left");

        result.ShouldNotBeNull();
        result!.SplitDirection.ShouldBe("left");
    }

    [Fact]
    public void Parse_BgLeft30Pct_SetsSplitWithPercent()
    {
        var result = MarpImageSyntax.Parse("bg left:30%");

        result.ShouldNotBeNull();
        result!.SplitDirection.ShouldBe("left");
        result.SplitPercent.ShouldBe(30);
    }

    [Fact]
    public void Parse_BgRight_SetsSplitDirection()
    {
        var result = MarpImageSyntax.Parse("bg right:60%");

        result.ShouldNotBeNull();
        result!.SplitDirection.ShouldBe("right");
        result.SplitPercent.ShouldBe(60);
    }

    // ── Sizing ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_Width_SetsWidth()
    {
        var result = MarpImageSyntax.Parse("w:200px");

        result.ShouldNotBeNull();
        result!.Width.ShouldBe("200px");
    }

    [Fact]
    public void Parse_Height_SetsHeight()
    {
        var result = MarpImageSyntax.Parse("h:100px");

        result.ShouldNotBeNull();
        result!.Height.ShouldBe("100px");
    }

    [Fact]
    public void Parse_WidthAndHeight_SetsBoth()
    {
        var result = MarpImageSyntax.Parse("w:200 h:100");

        result.ShouldNotBeNull();
        result!.Width.ShouldBe("200");
        result.Height.ShouldBe("100");
    }

    [Fact]
    public void Parse_PercentWidth_Works()
    {
        var result = MarpImageSyntax.Parse("w:50%");

        result.ShouldNotBeNull();
        result!.Width.ShouldBe("50%");
    }

    // ── Fit keyword ─────────────────────────────────────────────────

    [Fact]
    public void Parse_Fit_SetsFitTrue()
    {
        var result = MarpImageSyntax.Parse("fit");

        result.ShouldNotBeNull();
        result!.Fit.ShouldBeTrue();
    }

    // ── Plain alt text ──────────────────────────────────────────────

    [Fact]
    public void Parse_PlainAltText_ReturnsNull()
    {
        var result = MarpImageSyntax.Parse("My diagram");
        result.ShouldBeNull();
    }

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
        var result = MarpImageSyntax.Parse(null);
        result.ShouldBeNull();
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsNull()
    {
        var result = MarpImageSyntax.Parse("");
        result.ShouldBeNull();
    }

    // ── Mixed ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_BgCoverWithAlt_SetsRemainingAlt()
    {
        var result = MarpImageSyntax.Parse("bg cover My Image");

        result.ShouldNotBeNull();
        result!.IsBackground.ShouldBeTrue();
        result.BackgroundSize.ShouldBe("cover");
        result.RemainingAlt.ShouldBe("My Image");
    }

    [Fact]
    public void Parse_BgWithWidthAndHeight_CombinesProperly()
    {
        var result = MarpImageSyntax.Parse("bg w:300 h:200");

        result.ShouldNotBeNull();
        result!.IsBackground.ShouldBeTrue();
        result.Width.ShouldBe("300");
        result.Height.ShouldBe("200");
    }
}
