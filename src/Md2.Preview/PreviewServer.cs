// agent-notes: { ctx: "Embedded HTTP server for live HTML preview", deps: [HtmlPreviewRenderer, System.Net.HttpListener], state: active, last: "sato@2026-03-13" }

using System.Net;
using System.Text;
using System.Text.Json;

namespace Md2.Preview;

/// <summary>
/// Minimal HTTP server for serving HTML preview with hot-reload support.
/// </summary>
public sealed class PreviewServer : IDisposable
{
    private record ContentSnapshot(string FullHtml, string BodyHtml, long Version);

    private readonly HttpListener _listener;
    private volatile ContentSnapshot _content = new("", "", 0);
    private bool _disposed;

    // Binds to localhost only — intentional for security (no network exposure)
    public PreviewServer(int port = 0)
    {
        Port = port > 0 ? port : FindAvailablePort();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
    }

    public int Port { get; }
    public string Url => $"http://localhost:{Port}/";

    /// <summary>
    /// Updates the current HTML content and increments the version atomically.
    /// </summary>
    public void UpdateContent(string fullHtml, string bodyHtml)
    {
        _content = new ContentSnapshot(fullHtml, bodyHtml, _content.Version + 1);
    }

    /// <summary>
    /// Starts the server and processes requests until cancellation.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _listener.Start();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var contextTask = _listener.GetContextAsync();
                var completedTask = await Task.WhenAny(
                    contextTask,
                    Task.Delay(Timeout.Infinite, cancellationToken));

                if (completedTask != contextTask)
                    break;

                var context = await contextTask;
                await HandleRequestAsync(context);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when listener is stopped during cancellation
        }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        var snapshot = _content;

        try
        {
            switch (path)
            {
                case "/":
                    await WriteResponseAsync(context, snapshot.FullHtml, "text/html");
                    break;

                case "/reload":
                    var json = JsonSerializer.Serialize(new { version = snapshot.Version });
                    await WriteResponseAsync(context, json, "application/json");
                    break;

                case "/content":
                    await WriteResponseAsync(context, snapshot.BodyHtml, "text/html");
                    break;

                default:
                    context.Response.StatusCode = 404;
                    await WriteResponseAsync(context, "Not Found", "text/plain");
                    break;
            }
        }
        catch (HttpListenerException)
        {
            // Client disconnected — expected during normal browsing
        }
        catch (Exception)
        {
            try
            {
                context.Response.StatusCode = 500;
                await WriteResponseAsync(context, "Internal Server Error", "text/plain");
            }
            catch
            {
                // Response may already be in a broken state
            }
        }
    }

    private static async Task WriteResponseAsync(HttpListenerContext context, string content, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        context.Response.ContentType = $"{contentType}; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static int FindAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _listener.Close();
    }
}
