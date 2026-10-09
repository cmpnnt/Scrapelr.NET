using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET.Link;

namespace Scrapelr.NET;

/// <summary>
/// Wires a <see cref="Collector"/> to a <see cref="Processor"/> and runs a whole crawl.
/// </summary>
public static class Pipeline
{
    /// <summary>
    /// Crawls from <paramref name="uri"/> and saves every collected page, in crawl order, as a single
    /// PDF at <paramref name="outputFile"/>. Collection and rendering run concurrently.
    /// </summary>
    /// <param name="browser">The browser used to crawl and render. Its lifetime is owned by the caller.</param>
    /// <param name="uri">The URL to start crawling from.</param>
    /// <param name="outputFile">Where to save the merged PDF.</param>
    /// <param name="options">Crawl configuration.</param>
    public static async Task Run(IBrowser browser, string uri, string outputFile, CrawlOptions options)
    {
        var channel = Channel.CreateUnbounded<Node>();

        var collector = new Collector(browser, channel.Writer, options);
        var processor = new Processor(browser, channel.Reader);

        Task collect = collector.Collect(uri);
        Task process = processor.Process(outputFile);

        await Task.WhenAll(collect, process);
    }
}
