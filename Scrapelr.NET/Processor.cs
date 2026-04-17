using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET.Link;

namespace Scrapelr.NET;

/// <summary>
/// 
/// </summary>
public class Processor : IProcessor
{
    private readonly IBrowser _browser;
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
    
    /// <summary>
    /// 
    /// </summary>
    public async Task Process(string outputFile)
    {
        // TODO: Consider using an object pool somewhere to reduce the creation of page objects!!
        string systemTempDir = Path.GetTempPath();
        string pdfTempDir = Path.Combine(systemTempDir, RandomName(true));
        Console.WriteLine($"The temp dir is {pdfTempDir}");
        Directory.CreateDirectory(pdfTempDir);
        
        var counter = 0;
        
        List<Task> tasks = [];

        while (await _reader.WaitToReadAsync())
        {
            while (_reader.TryRead(out Node url))
            {
                Task t =  Task.Run(async () =>
                {
                    string uri = url.Url;
                    if (!string.IsNullOrWhiteSpace(uri))
                    {
                        Console.WriteLine(url);
                        IPage? page = await _browser.NewPageAsync();
                        await page.GoToAsync(uri);
                        await page.PdfAsync($"{pdfTempDir}{RandomName()}-{counter}.pdf");
                        counter = counter++;
                    }
                });
        
                tasks.Add(t);
            }
        }

        await Task.WhenAll(tasks);
    }

    private string RandomName(bool isDirectory = false)
    {
        var uuid = Guid.NewGuid().ToString();
        string[] parts = uuid.Split('-');
        return isDirectory ? parts[^1] + "/" : parts[^1];
    }
}
