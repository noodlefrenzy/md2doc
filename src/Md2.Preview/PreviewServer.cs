// agent-notes: { ctx: "Embedded HTTP server for live HTML preview", deps: [System.Net.Sockets], state: active, last: "sato@2026-03-14" }

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Md2.Preview;

/// <summary>
/// Minimal HTTP server for serving HTML preview with hot-reload support.
/// Uses raw TcpListener for maximum compatibility with port forwarding proxies.
/// Binds to localhost only — intentional for security (no network exposure).
/// </summary>
public sealed class PreviewServer : IDisposable
{
    private record ContentSnapshot(string FullHtml, string BodyHtml, long Version);

    private readonly TcpListener _listener;
    private volatile ContentSnapshot _content = new("", "", 0);
    private bool _disposed;

    public PreviewServer(int port = 0)
    {
        Port = port > 0 ? port : 0;
        _listener = new TcpListener(IPAddress.Loopback, Port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    public int Port { get; private set; }
    public string Url => $"http://localhost:{Port}/";

    /// <summary>
    /// Updates the current HTML content and increments the version atomically.
    /// </summary>
    public void UpdateContent(string fullHtml, string bodyHtml)
    {
        _content = new ContentSnapshot(fullHtml, bodyHtml, _content.Version + 1);
    }

    /// <summary>
    /// Processes requests until cancellation.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var acceptTask = _listener.AcceptTcpClientAsync(cancellationToken);
                var client = await acceptTask;
                // Fire and forget — handle each connection independently
                _ = HandleClientAsync(client);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when listener is stopped during cancellation
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using (client)
            await using (var stream = client.GetStream())
            {
                // Read request line to determine path
                var requestLine = await ReadLineAsync(stream);
                if (requestLine is null) return;

                // Skip remaining headers
                string? line;
                while ((line = await ReadLineAsync(stream)) is not null && line.Length > 0) { }

                var parts = requestLine.Split(' ');
                var path = parts.Length >= 2 ? parts[1] : "/";
                var snapshot = _content;

                switch (path)
                {
                    case "/":
                        await WriteHttpResponseAsync(stream, snapshot.FullHtml, "text/html");
                        break;
                    case "/reload":
                        var json = JsonSerializer.Serialize(new { version = snapshot.Version });
                        await WriteHttpResponseAsync(stream, json, "application/json");
                        break;
                    case "/content":
                        await WriteHttpResponseAsync(stream, snapshot.BodyHtml, "text/html");
                        break;
                    default:
                        await WriteHttpResponseAsync(stream, "Not Found", "text/plain", 404);
                        break;
                }
            }
        }
        catch
        {
            // Client disconnected or other I/O error — ignore
        }
    }

    private static async Task WriteHttpResponseAsync(NetworkStream stream, string body, string contentType, int statusCode = 200)
    {
        var statusText = statusCode == 200 ? "OK" : statusCode == 404 ? "Not Found" : "Error";
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header = $"HTTP/1.1 {statusCode} {statusText}\r\n"
                   + $"Content-Type: {contentType}; charset=utf-8\r\n"
                   + $"Content-Length: {bodyBytes.Length}\r\n"
                   + "Connection: close\r\n"
                   + "Content-Security-Policy: default-src 'self' https://cdn.jsdelivr.net; script-src https://cdn.jsdelivr.net 'unsafe-inline'; style-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; img-src 'self' data:\r\n"
                   + "\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);

        await stream.WriteAsync(headerBytes);
        await stream.WriteAsync(bodyBytes);
        await stream.FlushAsync();
    }

    /// <summary>Maximum length for an HTTP request line (8 KB). Connections exceeding this are dropped.</summary>
    internal const int MaxLineLength = 8192;

    private static async Task<string?> ReadLineAsync(NetworkStream stream)
    {
        var sb = new StringBuilder();
        var buffer = new byte[1];
        while (await stream.ReadAsync(buffer) > 0)
        {
            var c = (char)buffer[0];
            if (c == '\n') return sb.ToString().TrimEnd('\r');
            if (sb.Length >= MaxLineLength) return null;
            sb.Append(c);
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _listener.Stop();
    }
}
