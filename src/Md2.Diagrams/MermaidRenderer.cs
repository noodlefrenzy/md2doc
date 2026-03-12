// agent-notes: { ctx: "Renders Mermaid diagrams to PNG via Playwright", deps: [BrowserManager, DiagramCache, Microsoft.Playwright, Md2.Core.Exceptions], state: active, last: "sato@2026-03-12" }

using System.Net;
using System.Reflection;
using Md2.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace Md2.Diagrams;

/// <summary>
/// Renders Mermaid diagram source to PNG via Playwright/Chromium.
/// Uses content-hash caching to avoid redundant renders.
/// </summary>
public sealed class MermaidRenderer
{
    internal const string MermaidVersion = "11.13.0";
    private readonly BrowserManager _browserManager;
    private readonly DiagramCache _cache;
    private readonly ILogger<MermaidRenderer> _logger;
    private static readonly Lazy<string> MermaidJs = new(LoadMermaidJs);

    public MermaidRenderer(BrowserManager browserManager, DiagramCache cache, ILogger<MermaidRenderer>? logger = null)
    {
        _browserManager = browserManager ?? throw new ArgumentNullException(nameof(browserManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? NullLogger<MermaidRenderer>.Instance;
    }

    /// <summary>
    /// Renders Mermaid source to a PNG file. Returns the file path.
    /// Uses cache — repeated calls with the same source skip rendering.
    /// </summary>
    public async Task<string> RenderAsync(string mermaidSource, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mermaidSource);

        if (_cache.TryGetCached(mermaidSource, out var cachedPath))
        {
            _logger.LogDebug("Mermaid cache hit for diagram (hash: {Path})", Path.GetFileNameWithoutExtension(cachedPath));
            return cachedPath!;
        }

        _logger.LogInformation("Rendering Mermaid diagram ({Length} chars)", mermaidSource.Length);

        var browser = await _browserManager.GetBrowserAsync(cancellationToken);
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            DeviceScaleFactor = 2,
        });
        var page = await context.NewPageAsync();
        try
        {
            var html = BuildHtml(mermaidSource);
            await page.SetContentAsync(html, new PageSetContentOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            // Wait for Mermaid to render and check for errors
            var renderResult = await page.EvaluateAsync<string>(@"() => {
                return new Promise((resolve) => {
                    const check = () => {
                        const svg = document.querySelector('#mermaid-output svg');
                        if (svg) {
                            // Mermaid renders error diagrams with aria-roledescription='error'
                            if (svg.getAttribute('aria-roledescription') === 'error') {
                                const errorText = svg.querySelector('.error-text');
                                resolve('error:' + (errorText ? errorText.textContent : 'Invalid diagram syntax'));
                            } else {
                                resolve('ok');
                            }
                        } else {
                            return false;
                        }
                        return true;
                    };
                    if (check()) return;
                    const interval = setInterval(() => { if (check()) clearInterval(interval); }, 50);
                    setTimeout(() => {
                        clearInterval(interval);
                        if (!check()) resolve('error:Mermaid rendering timed out after 10s');
                    }, 10000);
                });
            }");

            if (renderResult.StartsWith("error:", StringComparison.Ordinal))
            {
                var errorMsg = renderResult["error:".Length..].Trim();
                throw new Md2ConversionException(
                    $"Mermaid rendering failed: {errorMsg}",
                    $"Failed to render Mermaid diagram: {errorMsg}");
            }

            // Screenshot the rendered SVG at 2x DPI
            var svgElement = await page.QuerySelectorAsync("#mermaid-output svg")
                ?? throw new Md2ConversionException(
                    "Mermaid rendered but SVG element not found",
                    "Failed to render Mermaid diagram: no SVG output produced");

            var pngBytes = await svgElement.ScreenshotAsync(new ElementHandleScreenshotOptions
            {
                Type = ScreenshotType.Png,
            });
            var path = _cache.Store(mermaidSource, pngBytes);
            _logger.LogInformation("Mermaid diagram rendered ({Bytes} bytes) -> {Path}", pngBytes.Length, Path.GetFileName(path));
            return path;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Md2ConversionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Md2ConversionException(
                $"Mermaid rendering failed: {ex.Message}",
                "Failed to render Mermaid diagram. Check that the diagram syntax is valid.",
                ex);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private static string BuildHtml(string mermaidSource)
    {
        var escapedSource = WebUtility.HtmlEncode(mermaidSource);

        return "<!DOCTYPE html>\n"
            + "<html><head><meta charset=\"utf-8\">\n"
            + "<style>body { margin: 0; padding: 16px; background: white; } #mermaid-output { display: inline-block; }</style>\n"
            + "</head><body>\n"
            + "<div id=\"mermaid-output\" class=\"mermaid\">\n"
            + escapedSource + "\n"
            + "</div>\n"
            + "<script>" + MermaidJs.Value + "</script>\n"
            + "<script>mermaid.initialize({ startOnLoad: true, theme: 'default' });</script>\n"
            + "</body></html>";
    }

    private static string LoadMermaidJs()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Md2.Diagrams.mermaid.min.js")
            ?? throw new InvalidOperationException("Mermaid JS resource not found in assembly. Ensure mermaid.min.js is embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
