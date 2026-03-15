// agent-notes: { ctx: "Tests for MARP theme → md2 preset mapping", deps: [Md2.Slides.MarpThemeMapper], state: active, last: "tara@2026-03-15" }

using Shouldly;
using Md2.Slides;

namespace Md2.Slides.Tests;

public class MarpThemeMapperTests
{
    [Theory]
    [InlineData("default", "default")]
    [InlineData("gaia", "default")]
    [InlineData("uncover", "minimal")]
    public void MapToPreset_KnownTheme_ReturnsMappedPreset(string marpTheme, string expectedPreset)
    {
        MarpThemeMapper.MapToPreset(marpTheme).ShouldBe(expectedPreset);
    }

    [Fact]
    public void MapToPreset_UnknownTheme_ReturnsNull()
    {
        MarpThemeMapper.MapToPreset("custom-theme").ShouldBeNull();
    }

    [Fact]
    public void MapToPreset_Null_ReturnsNull()
    {
        MarpThemeMapper.MapToPreset(null).ShouldBeNull();
    }

    [Fact]
    public void MapToPreset_CaseInsensitive()
    {
        MarpThemeMapper.MapToPreset("GAIA").ShouldBe("default");
        MarpThemeMapper.MapToPreset("Uncover").ShouldBe("minimal");
    }
}
