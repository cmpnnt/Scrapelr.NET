// See https://aka.ms/new-console-template for more information

using PuppeteerSharp;
using Scrapelr.NET;

await using IBrowser browser = await Collector.Setup();
const string uri = "https://learn.microsoft.com/en-us/dotnet/core/containers/overview?tabs=windows";
string[]? urls = await Collector.Collect(browser, uri);

if (urls is not null)
{
    foreach (string url in urls)
    {
        Console.WriteLine(url);
    }
}

/* TODO:
 1. channels and a thread pool for the parsing of each new URI in the queue
 2. add the results of Collector.Collect to a queue
 3. add ordering for the URLs to ensure that the PDFs are rendered in the same order the URLs were added to the queue
    - When all the parsing is done, sort the dictionary by the index and then stitch all the PDFs together
*/