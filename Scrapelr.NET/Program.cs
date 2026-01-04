// See https://aka.ms/new-console-template for more information

using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET;
using Scrapelr.NET.Link;

await using IBrowser browser = await Collector.Setup();
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
