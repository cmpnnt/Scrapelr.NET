// See https://aka.ms/new-console-template for more information

using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET;
using Scrapelr.NET.Link;

await using IBrowser browser = await Collector.Setup();

const string uri = "https://learn.microsoft.com/en-us/dotnet/core/containers/overview?tabs=windows";
var channel = Channel.CreateUnbounded<Node>();

/*
 * --root, -r
 * --pathOnly, -p (Default: true. `true` implies `domainOnly = true`, it's an error to set `domainOnly = false` if `pathOnly = true`)
 * --domainOnly, -d (Default: true)
 * --inputFile, -i
 * --outputFile -o (mandatory, error if not set)
 * --maxDepth -m (default 3)
 */
// The two spinners shown by SpectreConsole will be "Crawling..." and "Creating PDF..."

/* TODO: We need a flag to specify that the crawler should follow all URLs in an input text file.
 This will be false by default, and will be PathOnly by default. You can specify otherwise. So, if
 you provide a text file with a list of URLs and no other flags, it'll just give you a PDF with
 those pages. */

_ = Collector.Collect(browser, uri, channel.Writer, domainOnly: true, pathOnly: true);

ChannelReader<Node> reader = channel.Reader;
var processor = new Processor(browser, reader);
await processor.Process();

/* TODO:
 1. channels and a thread pool for the parsing of each new URI in the queue
*/
