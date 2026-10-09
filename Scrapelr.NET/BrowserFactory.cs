using PuppeteerSharp;

namespace Scrapelr.NET;

/// <summary>
/// Creates the headless browser used by the collector and processor. Kept separate from both
/// because the caller — not either of them — owns the browser's lifetime.
/// </summary>
public static class BrowserFactory
{
    /// <summary>
    /// Downloads a headless Chromium browser engine for using PuppeteerSharp.
    /// </summary>
    /// <returns>A PuppeteerSharp IBrowser instance. The caller is responsible for disposing it.</returns>
    public static async Task<IBrowser> Create()
    {
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        return await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
    }
}