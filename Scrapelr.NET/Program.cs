// See https://aka.ms/new-console-template for more information

using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET;
using Scrapelr.NET.Link;

await using IBrowser browser = await BrowserFactory.Create();

const string uri = "https://learn.microsoft.com/en-us/dotnet/core/containers/overview?tabs=windows";
const string outputFile = "/Users/chad/Downloads/output.pdf";
var channel = Channel.CreateUnbounded<Node>();

/*
 * --root, -r
 * --pathOnly, -p (Default: true. `true` implies `domainOnly = true`, it's an error to set `domainOnly = false` if `pathOnly = true`)
 *   Note: --pathOnly does strict path checking, so the child URL must be a substring of the parentURL, and the parentURL must be navigable (meaning not 404)
 * --domainOnly, -d (Default: true)
 * --inputFile, -i
 * --outputFile -o (mandatory, error if not set)
 * --maxDepth -m (default 1)
 */
// The two spinners shown by SpectreConsole will be "Crawling..." and "Creating PDF..."

/* TODO: We need a flag to specify that the crawler should follow all URLs in an input text file.
 This will be false by default, and will be PathOnly by default. You can specify otherwise. So, if
 you provide a text file with a list of URLs and no other flags, it'll just give you a PDF with
 those pages. */

/* TODO: Consider setting this up with dependency injection. Collector and Processor both implement an
   interface now, so either can be swapped out. */

var collector = new Collector(browser, channel.Writer, new CrawlOptions
{
    DomainOnly = true,
    PathOnly = true
});
_ = collector.Collect(uri);

ChannelReader<Node> reader = channel.Reader;
var processor = new Processor(browser, reader);
await processor.Process(outputFile);