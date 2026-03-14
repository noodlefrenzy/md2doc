// agent-notes: { ctx: "Tests for embedded HTTP preview server incl security headers", deps: [PreviewServer], state: red, last: "tara@2026-03-14" }

using System.Net;
using System.Net.Sockets;
using System.Text;
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

    // ── H-3: Content-Security-Policy header ───────────────────────────

    [Fact]
    public async Task Response_IncludesContentSecurityPolicyHeader()
    {
        _server.UpdateContent("<html>csp test</html>", "csp test");

        var response = await _client.GetAsync("/");

        response.Headers.TryGetValues("Content-Security-Policy", out var cspValues).ShouldBeTrue();
        var csp = string.Join("; ", cspValues!);
        csp.ShouldNotBeNullOrEmpty();
    }

    // ── M-1: No wildcard CORS ───────────────────────────────────────────

    [Fact]
    public async Task Response_DoesNotIncludeWildcardCorsHeader()
    {
        _server.UpdateContent("<html>cors test</html>", "cors test");

        var response = await _client.GetAsync("/");

        // The server should NOT send Access-Control-Allow-Origin: *
        if (response.Headers.TryGetValues("Access-Control-Allow-Origin", out var corsValues))
        {
            corsValues.ShouldNotContain("*");
        }
        // If the header is absent entirely, that's also acceptable
    }

    // ── M-2: ReadLineAsync max length ───────────────────────────────────

    [Fact]
    public async Task OversizedRequestLine_Returns414OrClosesConnection()
    {
        // Send a request line longer than 8192 bytes.
        // The server should reject it with 414 URI Too Long or close the connection,
        // NOT process the request as a normal 404 (which would mean ReadLineAsync
        // consumed the entire oversized line into memory).
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, _server.Port);
        await using var stream = tcpClient.GetStream();
        stream.ReadTimeout = 3000;
        stream.WriteTimeout = 3000;

        // Build a request line > 8192 bytes
        var oversizedPath = "/" + new string('A', 9000);
        var requestLine = $"GET {oversizedPath} HTTP/1.1\r\nHost: localhost\r\n\r\n";
        var requestBytes = Encoding.ASCII.GetBytes(requestLine);
        await stream.WriteAsync(requestBytes);
        await stream.FlushAsync();

        var responseBuffer = new byte[4096];
        var readTask = stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
        var completed = await Task.WhenAny(readTask, Task.Delay(3000));

        if (completed == readTask)
        {
            var bytesRead = await readTask;
            if (bytesRead > 0)
            {
                var response = Encoding.ASCII.GetString(responseBuffer, 0, bytesRead);
                // Must be 414 URI Too Long specifically — NOT 404.
                // A 404 means the server consumed the entire oversized line, which is the bug.
                response.ShouldContain("414");
            }
            // bytesRead == 0 means connection closed, which is also acceptable
        }
        else
        {
            Assert.Fail("Server did not respond or close connection within 3 seconds for oversized request");
        }
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
