// agent-notes: { ctx: "Orchestrates preview: server + renderer + watcher + browser", deps: [PreviewServer, HtmlPreviewRenderer, FileWatcher, Playwright], state: active, last: "sato@2026-03-13" }

using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Md2.Core.Pipeline;
using Microsoft.Playwright;
using System.Text;

namespace Md2.Preview;

/// <summary>
/// Orchestrates a live preview session: starts an HTTP server, opens Chromium,
/// watches the source file for changes, and re-renders on save.
/// </summary>
public sealed class PreviewSession : IAsyncDisposable
{
    private readonly string _filePath;
    private readonly ResolvedTheme _theme;
    private readonly MarkdownPipeline _pipeline;
    private readonly HtmlPreviewRenderer _renderer;
    private readonly PreviewServer _server;
    private readonly Action<string>? _log;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private FileWatcher? _watcher;
    private bool _disposed;

    private readonly bool _openBrowser;

    public PreviewSession(
        string filePath,
        ResolvedTheme theme,
        MarkdownPipeline pipeline,
        bool openBrowser = true,
        Action<string>? log = null)
    {
        _filePath = Path.GetFullPath(filePath);
        _theme = theme;
        _pipeline = pipeline;
        _renderer = new HtmlPreviewRenderer();
        _server = new PreviewServer();
        _openBrowser = openBrowser;
        _log = log;
    }

    public int Port => _server.Port;

    /// <summary>
    /// Starts the preview session and blocks until cancellation.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // Initial render
        RenderAndUpdate();
        _log?.Invoke($"Preview server starting at {_server.Url}");

        // Start server in background
        var serverTask = Task.Run(() => _server.RunAsync(cancellationToken), cancellationToken);

        // Open browser (unless --no-browser)
        if (_openBrowser)
        {
            _log?.Invoke("Opening browser...");
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false
            });
            var page = await _browser.NewPageAsync();
            await page.GotoAsync(_server.Url);
            _log?.Invoke($"Preview open at {_server.Url}");
        }
        else
        {
            _log?.Invoke($"Preview available at {_server.Url}");
        }

        // Start file watcher
        _watcher = new FileWatcher(_filePath, () =>
        {
            _log?.Invoke("File changed, re-rendering...");
            RenderAndUpdate();
        });

        // Wait for cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _log?.Invoke("Shutting down preview...");
        }

        await serverTask;
    }

    private void RenderAndUpdate()
    {
        try
        {
            var markdown = File.ReadAllText(_filePath);

            // Parse once, render both body and full page from the same AST
            var document = Markdown.Parse(markdown, _pipeline);

            var bodySb = new StringBuilder();
            using (var writer = new StringWriter(bodySb))
            {
                var htmlRenderer = new HtmlRenderer(writer);
                _pipeline.Setup(htmlRenderer);
                htmlRenderer.Render(document);
            }
            var bodyHtml = bodySb.ToString();

            var fullHtml = _renderer.Render(document, _theme, _pipeline);

            _server.UpdateContent(fullHtml, bodyHtml);
        }
        catch (IOException)
        {
            // File may be mid-write; watcher will fire again
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _watcher?.Dispose();

        if (_browser is not null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;

        _server.Dispose();
    }
}
