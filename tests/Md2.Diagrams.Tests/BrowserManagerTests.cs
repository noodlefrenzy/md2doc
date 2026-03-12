// agent-notes: { ctx: "Tests for BrowserManager Playwright lifecycle", deps: [Md2.Diagrams, Microsoft.Playwright], state: active, last: "tara@2026-03-12" }

using Shouldly;

namespace Md2.Diagrams.Tests;

public class BrowserManagerTests
{
    [Fact]
    public void IsChromiumInstalled_ReturnsTrue_WhenChromiumExists()
    {
        // This test depends on the devcontainer having Chromium installed
        // via PLAYWRIGHT_BROWSERS_PATH
        BrowserManager.IsChromiumInstalled().ShouldBeTrue();
    }

    [Fact]
    public async Task GetBrowserAsync_ReturnsBrowser()
    {
        await using var manager = new BrowserManager();

        var browser = await manager.GetBrowserAsync();

        browser.ShouldNotBeNull();
        browser.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public async Task GetBrowserAsync_ReturnsSameInstance_OnSecondCall()
    {
        await using var manager = new BrowserManager();

        var browser1 = await manager.GetBrowserAsync();
        var browser2 = await manager.GetBrowserAsync();

        browser1.ShouldBeSameAs(browser2);
    }

    [Fact]
    public async Task CreatePageAsync_ReturnsUsablePage()
    {
        await using var manager = new BrowserManager();

        var page = await manager.CreatePageAsync();

        page.ShouldNotBeNull();
        await page.SetContentAsync("<h1>Hello</h1>");
        var text = await page.TextContentAsync("h1");
        text.ShouldBe("Hello");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task DisposeAsync_ClosesBrowser()
    {
        var manager = new BrowserManager();
        var browser = await manager.GetBrowserAsync();
        browser.IsConnected.ShouldBeTrue();

        await manager.DisposeAsync();

        browser.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var manager = new BrowserManager();
        await manager.GetBrowserAsync();

        await manager.DisposeAsync();
        await manager.DisposeAsync(); // should not throw
    }
}
