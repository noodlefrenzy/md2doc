// agent-notes: { ctx: "Tests for MarpDirectiveCascader", deps: [Md2.Slides.Directives.MarpDirectiveCascader, Md2.Core.Slides], state: active, last: "tara@2026-03-15" }

using Md2.Slides.Directives;
using Shouldly;

namespace Md2.Slides.Tests.Directives;

public class MarpDirectiveCascaderTests
{
    // ── Global directives ───────────────────────────────────────────

    [Fact]
    public void Cascade_GlobalDirective_AppliesToAllSlides()
    {
        var directives = new List<MarpDirective>
        {
            new("backgroundColor", "#fff", MarpDirectiveScope.Global)
        };

        var result = MarpDirectiveCascader.Cascade(directives, 3);

        result.Count.ShouldBe(3);
        result[0].BackgroundColor.ShouldBe("#fff");
        result[1].BackgroundColor.ShouldBe("#fff");
        result[2].BackgroundColor.ShouldBe("#fff");
    }

    [Fact]
    public void Cascade_GlobalPaginate_AppliesToAllSlides()
    {
        var directives = new List<MarpDirective>
        {
            new("paginate", "true", MarpDirectiveScope.Global)
        };

        var result = MarpDirectiveCascader.Cascade(directives, 2);

        result[0].Paginate.ShouldBe(true);
        result[1].Paginate.ShouldBe(true);
    }

    // ── Local directives ────────────────────────────────────────────

    [Fact]
    public void Cascade_LocalDirective_PropagatesForward()
    {
        var directives = new List<MarpDirective>
        {
            new("class", "lead", MarpDirectiveScope.Local) { SlideIndex = 1 }
        };

        var result = MarpDirectiveCascader.Cascade(directives, 3);

        result[0].Class.ShouldBeNull();
        result[1].Class.ShouldBe("lead");
        result[2].Class.ShouldBe("lead"); // propagated forward
    }

    [Fact]
    public void Cascade_LocalDirective_OverriddenByLater()
    {
        var directives = new List<MarpDirective>
        {
            new("color", "red", MarpDirectiveScope.Local) { SlideIndex = 0 },
            new("color", "blue", MarpDirectiveScope.Local) { SlideIndex = 2 }
        };

        var result = MarpDirectiveCascader.Cascade(directives, 4);

        result[0].Color.ShouldBe("red");
        result[1].Color.ShouldBe("red"); // propagated
        result[2].Color.ShouldBe("blue"); // overridden
        result[3].Color.ShouldBe("blue"); // propagated
    }

    // ── Scoped directives ───────────────────────────────────────────

    [Fact]
    public void Cascade_ScopedDirective_AppliesOnlyToCurrentSlide()
    {
        var directives = new List<MarpDirective>
        {
            new("backgroundColor", "aqua", MarpDirectiveScope.Scoped) { SlideIndex = 1 }
        };

        var result = MarpDirectiveCascader.Cascade(directives, 3);

        result[0].BackgroundColor.ShouldBeNull();
        result[1].BackgroundColor.ShouldBe("aqua");
        result[2].BackgroundColor.ShouldBeNull(); // does NOT propagate
    }

    [Fact]
    public void Cascade_ScopedOverridesLocal_ForCurrentSlideOnly()
    {
        var directives = new List<MarpDirective>
        {
            new("color", "red", MarpDirectiveScope.Local) { SlideIndex = 0 },
            new("color", "green", MarpDirectiveScope.Scoped) { SlideIndex = 1 }
        };

        var result = MarpDirectiveCascader.Cascade(directives, 3);

        result[0].Color.ShouldBe("red");
        result[1].Color.ShouldBe("green"); // scoped override
        result[2].Color.ShouldBe("red"); // back to local propagation
    }

    // ── Mixed scenarios ─────────────────────────────────────────────

    [Fact]
    public void Cascade_GlobalPlusLocalPlusScoped_CorrectPrecedence()
    {
        var directives = new List<MarpDirective>
        {
            new("footer", "Global Footer", MarpDirectiveScope.Global),
            new("footer", "Local Footer", MarpDirectiveScope.Local) { SlideIndex = 1 },
            new("footer", "Scoped Footer", MarpDirectiveScope.Scoped) { SlideIndex = 2 }
        };

        var result = MarpDirectiveCascader.Cascade(directives, 4);

        result[0].Footer.ShouldBe("Global Footer");
        result[1].Footer.ShouldBe("Local Footer");
        result[2].Footer.ShouldBe("Scoped Footer"); // scoped wins for this slide
        result[3].Footer.ShouldBe("Local Footer"); // back to local propagation
    }

    [Fact]
    public void Cascade_HeaderAndFooterTogether()
    {
        var directives = new List<MarpDirective>
        {
            new("header", "My Header", MarpDirectiveScope.Global),
            new("footer", "My Footer", MarpDirectiveScope.Global)
        };

        var result = MarpDirectiveCascader.Cascade(directives, 2);

        result[0].Header.ShouldBe("My Header");
        result[0].Footer.ShouldBe("My Footer");
        result[1].Header.ShouldBe("My Header");
        result[1].Footer.ShouldBe("My Footer");
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Cascade_ZeroSlides_ReturnsEmpty()
    {
        var result = MarpDirectiveCascader.Cascade(new List<MarpDirective>(), 0);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Cascade_NoDirectives_ReturnsDefaultDirectives()
    {
        var result = MarpDirectiveCascader.Cascade(new List<MarpDirective>(), 2);

        result.Count.ShouldBe(2);
        result[0].BackgroundColor.ShouldBeNull();
        result[0].Class.ShouldBeNull();
        result[0].Paginate.ShouldBeNull();
    }

    [Fact]
    public void Cascade_NullDirectives_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            MarpDirectiveCascader.Cascade(null!, 3));
    }

    [Fact]
    public void Cascade_NegativeSlideCount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            MarpDirectiveCascader.Cascade(new List<MarpDirective>(), -1));
    }

    [Fact]
    public void Cascade_BackgroundImageDirective_Cascades()
    {
        var directives = new List<MarpDirective>
        {
            new("backgroundImage", "url(bg.jpg)", MarpDirectiveScope.Global)
        };

        var result = MarpDirectiveCascader.Cascade(directives, 2);

        result[0].BackgroundImage.ShouldBe("url(bg.jpg)");
        result[1].BackgroundImage.ShouldBe("url(bg.jpg)");
    }
}
