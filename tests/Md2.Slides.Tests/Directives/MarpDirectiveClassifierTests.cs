// agent-notes: { ctx: "Tests for MarpDirectiveClassifier", deps: [Md2.Slides.Directives.MarpDirectiveClassifier], state: active, last: "tara@2026-03-15" }

using Md2.Slides.Directives;
using Shouldly;

namespace Md2.Slides.Tests.Directives;

public class MarpDirectiveClassifierTests
{
    // ── ClassifySingle ──────────────────────────────────────────────

    [Fact]
    public void ClassifySingle_GlobalDirective_RemainsGlobal()
    {
        var directive = new MarpDirective("theme", "gaia", MarpDirectiveScope.Global);
        var result = MarpDirectiveClassifier.ClassifySingle(directive);

        result.Scope.ShouldBe(MarpDirectiveScope.Global);
        result.Key.ShouldBe("theme");
    }

    [Fact]
    public void ClassifySingle_UnderscorePrefix_BecomesScoped()
    {
        var directive = new MarpDirective("_backgroundColor", "aqua", MarpDirectiveScope.Local);
        var result = MarpDirectiveClassifier.ClassifySingle(directive);

        result.Scope.ShouldBe(MarpDirectiveScope.Scoped);
        result.Key.ShouldBe("backgroundColor");
    }

    [Fact]
    public void ClassifySingle_NoPrefix_RemainsLocal()
    {
        var directive = new MarpDirective("class", "lead", MarpDirectiveScope.Local);
        var result = MarpDirectiveClassifier.ClassifySingle(directive);

        result.Scope.ShouldBe(MarpDirectiveScope.Local);
        result.Key.ShouldBe("class");
    }

    [Fact]
    public void ClassifySingle_ScopedStripsUnderscore()
    {
        var directive = new MarpDirective("_color", "red", MarpDirectiveScope.Local);
        var result = MarpDirectiveClassifier.ClassifySingle(directive);

        result.Key.ShouldBe("color");
        result.Value.ShouldBe("red");
    }

    // ── Classify batch ──────────────────────────────────────────────

    [Fact]
    public void Classify_MixedDirectives_ClassifiesAll()
    {
        var directives = new List<MarpDirective>
        {
            new("theme", "gaia", MarpDirectiveScope.Global),
            new("class", "lead", MarpDirectiveScope.Local),
            new("_backgroundColor", "aqua", MarpDirectiveScope.Local),
        };

        var result = MarpDirectiveClassifier.Classify(directives);

        result.Count.ShouldBe(3);
        result[0].Scope.ShouldBe(MarpDirectiveScope.Global);
        result[1].Scope.ShouldBe(MarpDirectiveScope.Local);
        result[2].Scope.ShouldBe(MarpDirectiveScope.Scoped);
    }

    [Fact]
    public void Classify_EmptyList_ReturnsEmpty()
    {
        var result = MarpDirectiveClassifier.Classify(new List<MarpDirective>());
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Classify_NullInput_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            MarpDirectiveClassifier.Classify(null!));
    }

    // ── IsKnownDirective ────────────────────────────────────────────

    [Theory]
    [InlineData("theme")]
    [InlineData("paginate")]
    [InlineData("class")]
    [InlineData("backgroundColor")]
    [InlineData("header")]
    [InlineData("footer")]
    [InlineData("size")]
    [InlineData("headingDivider")]
    [InlineData("color")]
    public void IsKnownDirective_KnownKeys_ReturnsTrue(string key)
    {
        MarpDirectiveClassifier.IsKnownDirective(key).ShouldBeTrue();
    }

    [Fact]
    public void IsKnownDirective_UnderscorePrefixed_ReturnsTrue()
    {
        MarpDirectiveClassifier.IsKnownDirective("_class").ShouldBeTrue();
    }

    [Fact]
    public void IsKnownDirective_UnknownKey_ReturnsFalse()
    {
        MarpDirectiveClassifier.IsKnownDirective("customThing").ShouldBeFalse();
    }

    [Fact]
    public void IsKnownDirective_CaseInsensitive()
    {
        MarpDirectiveClassifier.IsKnownDirective("THEME").ShouldBeTrue();
        MarpDirectiveClassifier.IsKnownDirective("BackgroundColor").ShouldBeTrue();
    }
}
