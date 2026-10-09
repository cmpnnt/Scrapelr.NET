using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET.Link;

namespace Scrapelr.NET;

/// <summary>
/// Wires an <see cref="ICollector"/> to an <see cref="IProcessor"/> and runs a whole crawl.
/// </summary>
/// <remarks>
/// Both are created per run around a fresh channel, so they're injected as factories rather than
/// instances. Use <see cref="Create"/> for the browser-backed defaults.
/// </remarks>
public sealed class Pipeline
{
    private readonly Func<ChannelWriter<Node>, ICollector> _createCollector;
    private readonly Func<ChannelReader<Node>, IProcessor> _createProcessor;

    /// <summary>
    /// Creates a pipeline from custom collector and processor factories.
    /// </summary>
    /// <param name="createCollector">Creates the collector that publishes links to the given writer.</param>
    /// <param name="createProcessor">Creates the processor that consumes links from the given reader.</param>
    public Pipeline(
        Func<ChannelWriter<Node>, ICollector> createCollector,
        Func<ChannelReader<Node>, IProcessor> createProcessor)
    {
        ArgumentNullException.ThrowIfNull(createCollector);
        ArgumentNullException.ThrowIfNull(createProcessor);

        _createCollector = createCollector;
        _createProcessor = createProcessor;
    }

    /// <summary>
    /// Creates a pipeline that crawls with a <see cref="Collector"/> and renders with a <see cref="Processor"/>.
    /// </summary>
    /// <param name="browser">The browser used to crawl and render. Its lifetime is owned by the caller.</param>
    /// <param name="options">Crawl configuration.</param>
    /// <param name="format">How pages are rendered and merged. Defaults to <see cref="PdfOutputFormat"/>.</param>
    public static Pipeline Create(IBrowser browser, CrawlOptions options, IOutputFormat? format = null) => new(
        writer => new Collector(browser, writer, options),
        reader => new Processor(browser, reader, format));

    /// <summary>
    /// Crawls from each of <paramref name="uris"/> and saves every collected page, in crawl order, as a
    /// single document at <paramref name="outputFile"/>. Collection and processing run concurrently.
    /// </summary>
    /// <param name="uris">The URLs to start crawling from.</param>
    /// <param name="outputFile">Where to save the merged document.</param>
    public async Task Run(IEnumerable<string> uris, string outputFile)
    {
        var channel = Channel.CreateUnbounded<Node>();

        ICollector collector = _createCollector(channel.Writer);
        IProcessor processor = _createProcessor(channel.Reader);

        Task collect = collector.Collect(uris);
        Task process = processor.Process(outputFile);

        await Task.WhenAll(collect, process);
    }
}
