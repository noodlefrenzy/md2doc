// agent-notes: { ctx: "Tests for MarpExtensionParser (md2 extensions)", deps: [Md2.Slides.MarpExtensionParser], state: active, last: "tara@2026-03-15" }

using Md2.Slides;
using Shouldly;

namespace Md2.Slides.Tests;

public class MarpExtensionParserTests
{
    // ── Basic parsing ───────────────────────────────────────────────

    [Fact]
    public void TryParse_BuildDirective_ReturnsBuild()
    {
        var result = MarpExtensionParser.TryParse("<!-- md2: { build: \"bullets\" } -->");

        result.ShouldNotBeNull();
        result!.Build.ShouldBe("bullets");
    }

    [Fact]
    public void TryParse_LayoutDirective_ReturnsLayout()
    {
        var result = MarpExtensionParser.TryParse("<!-- md2: { layout: \"two-column\" } -->");

        result.ShouldNotBeNull();
        result!.Layout.ShouldBe("two-column");
    }

    [Fact]
    public void TryParse_TransitionDirective_ReturnsTransition()
    {
        var result = MarpExtensionParser.TryParse("<!-- md2: { transition: \"fade\" } -->");

        result.ShouldNotBeNull();
        result!.Transition.ShouldBe("fade");
    }

    [Fact]
    public void TryParse_MultipleProperties_ReturnsAll()
    {
        var result = MarpExtensionParser.TryParse("<!-- md2: { build: \"bullets\", layout: \"content\" } -->");

        result.ShouldNotBeNull();
        result!.Build.ShouldBe("bullets");
        result.Layout.ShouldBe("content");
    }

    // ── Extra properties ────────────────────────────────────────────

    [Fact]
    public void TryParse_UnknownProperty_GoesToExtra()
    {
        var result = MarpExtensionParser.TryParse("<!-- md2: { customThing: \"value\" } -->");

        result.ShouldNotBeNull();
        result!.Extra.ShouldNotBeNull();
        result.Extra!["customThing"].ToString().ShouldBe("value");
    }

    // ── Non-md2 comments ────────────────────────────────────────────

    [Fact]
    public void TryParse_RegularComment_ReturnsNull()
    {
        var result = MarpExtensionParser.TryParse("<!-- just a comment -->");
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParse_MarpDirective_ReturnsNull()
    {
        var result = MarpExtensionParser.TryParse("<!-- class: invert -->");
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParse_NullInput_ReturnsNull()
    {
        var result = MarpExtensionParser.TryParse(null!);
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParse_EmptyInput_ReturnsNull()
    {
        var result = MarpExtensionParser.TryParse("");
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParse_InvalidYaml_ReturnsNull()
    {
        var result = MarpExtensionParser.TryParse("<!-- md2: { invalid:: yaml::: } -->");
        result.ShouldBeNull();
    }

    // ── IsMd2Extension ──────────────────────────────────────────────

    [Fact]
    public void IsMd2Extension_ValidExtension_ReturnsTrue()
    {
        MarpExtensionParser.IsMd2Extension("<!-- md2: { build: \"bullets\" } -->").ShouldBeTrue();
    }

    [Fact]
    public void IsMd2Extension_RegularComment_ReturnsFalse()
    {
        MarpExtensionParser.IsMd2Extension("<!-- just a comment -->").ShouldBeFalse();
    }

    [Fact]
    public void IsMd2Extension_NullInput_ReturnsFalse()
    {
        MarpExtensionParser.IsMd2Extension(null!).ShouldBeFalse();
    }
}
