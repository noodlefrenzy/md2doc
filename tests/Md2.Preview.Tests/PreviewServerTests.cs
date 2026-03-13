// agent-notes: { ctx: "Tests for embedded HTTP preview server", deps: [PreviewServer], state: active, last: "tara@2026-03-13" }

using System.Net;
using System.Text.Json;
using Md2.Preview;
using Shouldly;

namespace Md2.Preview.Tests;

public class PreviewServerTests : IDisposable
{
    private readonly PreviewServer _server;
    private readonly CancellationTokenSource _cts;
    private readonly HttpClient _client;
    private readonly Task _serverTask;

    public PreviewServerTests()
    {
        _server = new PreviewServer();
        _server.UpdateContent("<html>initial</html>", "initial");
        _cts = new CancellationTokenSource();
        _serverTask = Task.Run(() => _server.RunAsync(_cts.Token));
        _client = new HttpClient { BaseAddress = new Uri(_server.Url) };
        // Give the server a moment to start
        Thread.Sleep(100);
    }

    [Fact]
    public async Task RootEndpoint_ReturnsFullHtml()
    {
        _server.UpdateContent("<html>full page</html>", "body only");

        var response = await _client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe("<html>full page</html>");
    }

    [Fact]
    public async Task ContentEndpoint_ReturnsBodyHtml()
    {
        _server.UpdateContent("<html>full</html>", "<p>body</p>");

        var response = await _client.GetAsync("/content");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe("<p>body</p>");
    }

    [Fact]
    public async Task ReloadEndpoint_ReturnsVersionJson()
    {
        var response = await _client.GetAsync("/reload");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("version").GetInt64().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateContent_IncrementsVersion()
    {
        var resp1 = await _client.GetStringAsync("/reload");
        var v1 = JsonDocument.Parse(resp1).RootElement.GetProperty("version").GetInt64();

        _server.UpdateContent("<html>v2</html>", "v2");

        var resp2 = await _client.GetStringAsync("/reload");
        var v2 = JsonDocument.Parse(resp2).RootElement.GetProperty("version").GetInt64();

        v2.ShouldBeGreaterThan(v1);
    }

    [Fact]
    public async Task UnknownPath_Returns404()
    {
        var response = await _client.GetAsync("/unknown");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public void Port_IsAssigned()
    {
        _server.Port.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Url_ContainsPort()
    {
        _server.Url.ShouldContain(_server.Port.ToString());
        _server.Url.ShouldStartWith("http://localhost:");
    }

    [Fact]
    public async Task ContentType_IsUtf8Html()
    {
        _server.UpdateContent("<html>test</html>", "test");

        var response = await _client.GetAsync("/");

        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        response.Content.Headers.ContentType.CharSet.ShouldBe("utf-8");
    }

    [Fact]
    public async Task ReloadEndpoint_ContentType_IsJson()
    {
        var response = await _client.GetAsync("/reload");

        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _client.Dispose();
        try { _serverTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _server.Dispose();
        _cts.Dispose();
    }
}
