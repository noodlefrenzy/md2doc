// agent-notes: { ctx: "Red-phase tests for MermaidRenderer PNG rendering with cache and theme", deps: [Md2.Diagrams.MermaidRenderer, Md2.Diagrams.MermaidThemeConfig, Md2.Diagrams.DiagramCache, Md2.Diagrams.BrowserManager, Md2.Core.Exceptions, Shouldly], state: red, last: "sato@2026-03-13" }

using Microsoft.Extensions.Logging.Abstractions;
using Md2.Core.Exceptions;
using Shouldly;

namespace Md2.Diagrams.Tests;

[Trait("Category", "Integration")]
public class MermaidRendererTests : IAsyncDisposable
{
    private readonly string _cacheDir;
    private readonly BrowserManager _browserManager;
    private readonly DiagramCache _cache;
    private readonly MermaidRenderer _renderer;

    public MermaidRendererTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "md2-mermaid-test-" + Guid.NewGuid().ToString("N"));
        _browserManager = new BrowserManager();
        _cache = new DiagramCache(_cacheDir);
        _renderer = new MermaidRenderer(_browserManager, _cache, NullLogger<MermaidRenderer>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _browserManager.DisposeAsync();
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public async Task RenderAsync_ReturnsFilePath_ForValidDiagram()
    {
        var mermaidSource = "graph TD;\n    A-->B;\n    B-->C;";

        var path = await _renderer.RenderAsync(mermaidSource);

        path.ShouldNotBeNullOrEmpty();
        File.Exists(path).ShouldBeTrue();
        new FileInfo(path).Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderAsync_ReturnsCachedPath_OnSecondCall()
    {
        var mermaidSource = "graph TD;\n    X-->Y;";

        var path1 = await _renderer.RenderAsync(mermaidSource);
        var path2 = await _renderer.RenderAsync(mermaidSource);

        // Same path means cache hit
        path1.ShouldBe(path2);
    }

    [Fact]
    public async Task RenderAsync_ThrowsMd2ConversionException_ForInvalidSyntax()
    {
        var garbageInput = "this is not valid mermaid syntax }{][";

        await Should.ThrowAsync<Md2ConversionException>(
            () => _renderer.RenderAsync(garbageInput));
    }

    [Fact]
    public async Task RenderAsync_ProducesPng_WithExpectedMagicBytes()
    {
        var mermaidSource = "graph LR;\n    Start-->End;";

        var path = await _renderer.RenderAsync(mermaidSource);

        var bytes = await File.ReadAllBytesAsync(path);
        // PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
        bytes.Length.ShouldBeGreaterThan(8);
        bytes[0].ShouldBe((byte)0x89);
        bytes[1].ShouldBe((byte)0x50); // P
        bytes[2].ShouldBe((byte)0x4E); // N
        bytes[3].ShouldBe((byte)0x47); // G
        bytes[4].ShouldBe((byte)0x0D);
        bytes[5].ShouldBe((byte)0x0A);
        bytes[6].ShouldBe((byte)0x1A);
        bytes[7].ShouldBe((byte)0x0A);
    }

    [Fact]
    public async Task RenderAsync_DifferentDiagrams_ProduceDifferentFiles()
    {
        var source1 = "graph TD;\n    A-->B;";
        var source2 = "graph LR;\n    C-->D;\n    D-->E;";

        var path1 = await _renderer.RenderAsync(source1);
        var path2 = await _renderer.RenderAsync(source2);

        path1.ShouldNotBe(path2);
    }

    // -----------------------------------------------------------------------
    // BuildHtml theme-aware tests (issue #89)
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildHtml_WithThemeConfig_ContainsBaseTheme()
    {
        var themeConfig = new MermaidThemeConfig
        {
            PrimaryColor = "1B3A5C",
            SecondaryColor = "4A90D9",
            TextColor = "333333",
            FontFamily = "Calibri, sans-serif",
            FontSizePx = 14.67,
        };
        var source = "graph TD; A-->B;";

        var html = MermaidRenderer.BuildHtml(source, themeConfig);

        html.ShouldContain("theme: 'base'");
        html.ShouldNotContain("theme: 'default'");
    }

    [Fact]
    public void BuildHtml_WithThemeConfig_ContainsMappedThemeVariables()
    {
        var themeConfig = new MermaidThemeConfig
        {
            PrimaryColor = "FF0000",
            SecondaryColor = "00FF00",
            TextColor = "0000FF",
            PrimaryTextColor = "FFFFFF",
            FontFamily = "Georgia, sans-serif",
            FontSizePx = 16.0,
        };
        var source = "graph TD; A-->B;";

        var html = MermaidRenderer.BuildHtml(source, themeConfig);

        html.ShouldContain("themeVariables");
        html.ShouldContain("#FF0000");
        html.ShouldContain("#00FF00");
        html.ShouldContain("#0000FF");
        html.ShouldContain("Georgia");
    }

    [Fact]
    public void BuildHtml_WithNullThemeConfig_StillProducesValidHtml()
    {
        var source = "graph TD; A-->B;";

        var html = MermaidRenderer.BuildHtml(source, themeConfig: null);

        html.ShouldContain("mermaid.initialize");
        html.ShouldContain("mermaid-output");
        html.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildHtml_WithThemeConfig_ContainsPrimaryTextColor()
    {
        var themeConfig = new MermaidThemeConfig
        {
            PrimaryColor = "1B3A5C",
            PrimaryTextColor = "FAFAFA",
        };
        var source = "graph TD; A-->B;";

        var html = MermaidRenderer.BuildHtml(source, themeConfig);

        html.ShouldContain("#FAFAFA");
    }

    [Fact]
    public void BuildHtml_WithThemeConfig_ContainsFontSizeAsPx()
    {
        var themeConfig = new MermaidThemeConfig
        {
            FontSizePx = 16.0,
        };
        var source = "graph TD; A-->B;";

        var html = MermaidRenderer.BuildHtml(source, themeConfig);

        html.ShouldContain("16px");
    }
}
