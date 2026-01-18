using PuppeteerSharp;
using Scrapelr.NET.Link;

namespace Scrapelr.NET;

/// <summary>
/// 
/// </summary>
public class Processor
{
    // TODO: Wrap this in a Task.Run, probably in Program.cs, and then figure out how to 
    // move it into this file. Probably as a "fire and forget" method that gets called in Program.cs
    /// <summary>
    /// 
    /// </summary>
    /// <param name="url"></param>
    /// <param name="browser"></param>
    public static void Process(Node url, IBrowser browser)
    {
        
        // TODO: Consider using an object pool somewhere to reduce the creation of page objects
        
        // IPage? page = await browser.NewPageAsync();
        // await page.GoToAsync("http://www.google.com");
        
        // TODO: The PDF creation should use Task.Run
        Console.WriteLine(url);
        
        // const string outputFile = "/Users/chad/Downloads/output.pdf";
        // await page.PdfAsync(outputFile);
    }
}