using System;
using System.Threading.Channels;
using PuppeteerSharp;

namespace Scrapelr.NET;

public class Processor
{
    // TODO: Wrap this in a Task.Run, probably in Program.cs, and then figure out how to 
    // move it into this file. Probably as a "fire and forget" method that gets called in Program.cs
    public static void Process(string url, IBrowser browser)
    {
        // IPage? page = await browser.NewPageAsync();
        // await page.GoToAsync("http://www.google.com");
        
        // TODO: The PDF creation should use Task.Run
        Console.WriteLine(url);
        
        // const string outputFile = "/Users/chad/Downloads/output.pdf";
        // await page.PdfAsync(outputFile);
    }
}