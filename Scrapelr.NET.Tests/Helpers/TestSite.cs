using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Scrapelr.NET.Tests.Helpers;

/// <summary>
/// Serves a fixed set of HTML pages from a local port so the real browser has something to crawl.
/// </summary>
internal sealed class TestSite : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly IReadOnlyDictionary<string, string> _pages;

    /// <param name="pages">HTML bodies keyed by absolute path. Other paths return 404.</param>
    public TestSite(IReadOnlyDictionary<string, string> pages)
    {
        _pages = pages;
        BaseUrl = $"http://127.0.0.1:{FreePort()}/";
        _listener.Prefixes.Add(BaseUrl);
        _listener.Start();
        _ = Serve();
    }

    public string BaseUrl { get; }

    public string Url(string path) => new Uri(new Uri(BaseUrl), path).AbsoluteUri;

    public void Dispose() => _listener.Close();

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            if (_pages.TryGetValue(context.Request.Url!.AbsolutePath, out string? html))
            {
                byte[] body = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.OutputStream.WriteAsync(body);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
            context.Response.Close();
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
