// See https://aka.ms/new-console-template for more information

using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET;

await using IBrowser browser = await Collector.Setup();
const string uri = "https://learn.microsoft.com/en-us/dotnet/core/containers/overview?tabs=windows";
var channel = Channel.CreateUnbounded<string?>();

Collector.Collect(browser, uri, channel.Writer);
ChannelReader<string?> reader = channel.Reader;

List<Task> tasks = [];

while (await reader.WaitToReadAsync())
{
    while (reader.TryRead(out string? url))
    {
        Task t =  Task.Run(() =>
        {
            if (!string.IsNullOrWhiteSpace(url))
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
 2. add the results of Collector.Collect to a queue
 3. add ordering for the URLs to ensure that the PDFs are rendered in the same order the URLs were added to the queue
    - When all the parsing is done, sort the dictionary by the index and then stitch all the PDFs together
*/