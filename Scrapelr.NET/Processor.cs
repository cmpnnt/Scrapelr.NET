using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET.Link;

namespace Scrapelr.NET;

/// <summary>
/// 
/// </summary>
public class Processor
{
    private IBrowser _browser;
    private readonly ChannelReader<Node> _reader;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="browser"></param>
    /// <param name="reader"></param>
    public Processor(IBrowser browser, ChannelReader<Node> reader)
    {
        _browser = browser;
        _reader = reader;
    }
    
    // TODO: Wrap this in a Task.Run, probably in Program.cs, and then figure out how to 
    // move it into this file. Probably as a "fire and forget" method that gets called in Program.cs
    /// <summary>
    /// 
    /// </summary>
    public async Task Process()
    {
        // TODO: Consider using an object pool somewhere to reduce the creation of page objects!!
        
        // IPage? page = await browser.NewPageAsync();
        // await page.GoToAsync("http://www.google.com");
        
        List<Task> tasks = [];

        while (await _reader.WaitToReadAsync())
        {
            while (_reader.TryRead(out Node url))
            {
                Task t =  Task.Run(() =>
                {
                    if (!string.IsNullOrWhiteSpace(url.Url))
                    {
                        Console.WriteLine(url);
                    }
                });
        
                tasks.Add(t);
            }
        }

        await Task.WhenAll(tasks);
        
        // const string outputFile = "/Users/chad/Downloads/output.pdf";
        // await page.PdfAsync(outputFile);
        
        /* TODO: Get system temp directory. Create a temp folder for these PDFS, which should be named
        in such a way that they can be stitched together in order. */
    }
}
