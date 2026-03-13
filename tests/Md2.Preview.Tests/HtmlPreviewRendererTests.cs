// agent-notes: { ctx: "Tests for HTML preview renderer", deps: [HtmlPreviewRenderer, ResolvedTheme, Markdig], state: active, last: "tara@2026-03-13" }

using Markdig;
using Markdig.Syntax;
using Md2.Core.Pipeline;
using Md2.Preview;
using Shouldly;

namespace Md2.Preview.Tests;

public class HtmlPreviewRendererTests
{
    private readonly HtmlPreviewRenderer _renderer = new();
    private readonly ResolvedTheme _theme = ResolvedTheme.CreateDefault();
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().Build();

    [Fact]
    public void Render_ProducesCompleteHtmlPage()
    {
        var doc = Markdown.Parse("# Hello", _pipeline);

        var html = _renderer.Render(doc, _theme, _pipeline);

        html.ShouldContain("<!DOCTYPE html>");
        html.ShouldContain("<html lang=\"en\">");
        html.ShouldContain("</html>");
    }

    [Fact]
    public void Render_ContainsBodyContent()
    {
        var doc = Markdown.Parse("# Hello World", _pipeline);

        var html = _renderer.Render(doc, _theme, _pipeline);

        html.ShouldContain("<h1>Hello World</h1>");
    }

    [Fact]
    public void Render_IncludesThemeCssVariables()
    {
        var html = _renderer.RenderFromSource("test", _theme, _pipeline);

        html.ShouldContain($"--primary: #{_theme.PrimaryColor}");
        html.ShouldContain($"--secondary: #{_theme.SecondaryColor}");
        html.ShouldContain($"--body-text: #{_theme.BodyTextColor}");
    }

    [Fact]
    public void Render_IncludesThemeFonts()
    {
        var html = _renderer.RenderFromSource("test", _theme, _pipeline);

        html.ShouldContain(_theme.BodyFont);
        html.ShouldContain(_theme.HeadingFont);
        html.ShouldContain(_theme.MonoFont);
    }

    [Fact]
    public void Render_IncludesReloadScript()
    {
        var html = _renderer.RenderFromSource("test", _theme, _pipeline);

        html.ShouldContain("fetch('/reload')");
        html.ShouldContain("fetch('/content')");
    }

    [Fact]
    public void Render_WrapsContentInDiv()
    {
        var html = _renderer.RenderFromSource("hello", _theme, _pipeline);

        html.ShouldContain("<div id=\"content\">");
    }

    [Fact]
    public void Render_UsesCustomThemeColors()
    {
        var theme = new ResolvedTheme { PrimaryColor = "FF0000", SecondaryColor = "00FF00" };

        var html = _renderer.RenderFromSource("test", theme, _pipeline);

        html.ShouldContain("--primary: #FF0000");
        html.ShouldContain("--secondary: #00FF00");
    }

    [Fact]
    public void Render_ThrowsOnNullDocument()
    {
        Should.Throw<ArgumentNullException>(() =>
            _renderer.Render(null!, _theme, _pipeline));
    }

    [Fact]
    public void Render_ThrowsOnNullTheme()
    {
        var doc = Markdown.Parse("test", _pipeline);

        Should.Throw<ArgumentNullException>(() =>
            _renderer.Render(doc, null!, _pipeline));
    }

    [Fact]
    public void Render_ThrowsOnNullPipeline()
    {
        var doc = Markdown.Parse("test", _pipeline);

        Should.Throw<ArgumentNullException>(() =>
            _renderer.Render(doc, _theme, null!));
    }

    [Fact]
    public void RenderFromSource_ParsesAndRenders()
    {
        var html = _renderer.RenderFromSource("**bold text**", _theme, _pipeline);

        html.ShouldContain("<strong>bold text</strong>");
    }

    [Fact]
    public void Render_IncludesHeadingSizes()
    {
        var html = _renderer.RenderFromSource("# H1", _theme, _pipeline);

        html.ShouldContain($"{_theme.Heading1Size}pt");
        html.ShouldContain($"{_theme.Heading2Size}pt");
    }

    [Fact]
    public void Render_IncludesTableStyles()
    {
        var html = _renderer.RenderFromSource("test", _theme, _pipeline);

        html.ShouldContain("--table-header-bg:");
        html.ShouldContain("--table-border:");
        html.ShouldContain("--table-alt-row:");
    }

    [Fact]
    public void Render_SanitizesFontNames()
    {
        var theme = new ResolvedTheme { BodyFont = "Evil'; } body { background: red; } .x { font: '" };

        var html = _renderer.RenderFromSource("test", theme, _pipeline);

        // The injected CSS should be stripped — no closing single quotes or braces from font name
        html.ShouldNotContain("background: red");
    }

    [Fact]
    public void Render_SanitizesColorHex()
    {
        var theme = new ResolvedTheme { PrimaryColor = "FF0000; } body { display: none" };

        var html = _renderer.RenderFromSource("test", theme, _pipeline);

        html.ShouldNotContain("display: none");
        html.ShouldContain("FF0000");
    }
}
