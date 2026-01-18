// See https://aka.ms/new-console-template for more information

using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET;
using Scrapelr.NET.Link;

await using IBrowser browser = await Collector.Setup();

/* TODO: We need a flag to specify that the crawler should follow all URLs in an input text file.
 This will be false by default, and will be PathOnly by default. You can specify otherwise. So, if
 you provide a text file with a list of URLs and no other flags, it'll just give you a PDF with 
 those pages. */

const string uri = "https://learn.microsoft.com/en-us/dotnet/core/containers/overview?tabs=windows";
var channel = Channel.CreateUnbounded<Node>();

Collector.Collect(browser, uri, channel.Writer);
ChannelReader<Node> reader = channel.Reader;

List<Task> tasks = [];

while (await reader.WaitToReadAsync())
{
    while (reader.TryRead(out Node url))
    {
        Task t =  Task.Run(() =>
        {
            if (!string.IsNullOrWhiteSpace(url.Url))
            {
                Processor.Process(url, browser);
            }
        });
        
        tasks.Add(t);
    }
}

await Task.WhenAll(tasks);

/* TODO:
 1. channels and a thread pool for the parsing of each new URI in the queue
*/
