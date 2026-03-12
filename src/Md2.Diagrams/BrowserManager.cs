// agent-notes: { ctx: "Shared Playwright browser lifecycle for Mermaid/Math", deps: [Microsoft.Playwright, ILogger, Md2.Core.Exceptions], state: active, last: "sato@2026-03-12" }

using Md2.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace Md2.Diagrams;

/// <summary>
/// Manages a shared Playwright Chromium browser instance.
/// One instance per md2 invocation, reused across all diagrams.
/// </summary>
public sealed class BrowserManager : IAsyncDisposable
{
    /// <summary>Maximum time to wait for browser launch (30 seconds).</summary>
    public const int LaunchTimeoutMs = 30_000;

    /// <summary>Default timeout for page-level operations (30 seconds).</summary>
    public const int PageTimeoutMs = 30_000;

    private readonly ILogger<BrowserManager> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed;

    public BrowserManager()
        : this(NullLogger<BrowserManager>.Instance)
    {
    }

    public BrowserManager(ILogger<BrowserManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns true if a Chromium installation is detected at the Playwright browsers path.
    /// </summary>
    public static bool IsChromiumInstalled()
    {
        var browsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "ms-playwright");

        if (!Directory.Exists(browsersPath))
            return false;

        return Directory.GetDirectories(browsersPath, "chromium-*").Length > 0;
    }

    /// <summary>
    /// Installs Chromium via Playwright CLI. Returns exit code (0 = success).
    /// </summary>
    public static async Task<int> InstallChromiumAsync(CancellationToken cancellationToken = default)
    {
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        return await Task.FromResult(exitCode);
    }

    /// <summary>
    /// Ensures the browser is launched and returns it. Thread-safe via lazy init.
    /// </summary>
    public async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_browser is not null)
            return _browser;

        if (!IsChromiumInstalled())
        {
            throw new Md2ConversionException(
                "Chromium is not installed at the expected Playwright browsers path.",
                "Chromium is not installed. Run 'playwright install chromium' or 'md2 doctor' for help.");
        }

        _logger.LogInformation("Launching Chromium browser");
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Timeout = LaunchTimeoutMs,
            });
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase))
        {
            throw new Md2ConversionException(
                $"Chromium executable not found: {ex.Message}",
                "Chromium is not installed. Run 'playwright install chromium' or 'md2 doctor' for help.",
                ex);
        }
        catch (TimeoutException ex)
        {
            throw new Md2ConversionException(
                $"Browser launch timed out after {LaunchTimeoutMs}ms: {ex.Message}",
                "Browser launch timed out. This may indicate a resource-constrained environment.",
                ex);
        }

        _logger.LogInformation("Chromium browser launched ({ContextCount} contexts)", _browser.Contexts.Count);
        return _browser;
    }

    /// <summary>
    /// Creates a new isolated page context for rendering.
    /// </summary>
    public async Task<IPage> CreatePageAsync(CancellationToken cancellationToken = default)
    {
        var browser = await GetBrowserAsync(cancellationToken);
        var context = await browser.NewContextAsync();
        return await context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_browser is not null)
        {
            _logger.LogInformation("Closing Chromium browser");
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
    }
}
