using PuppeteerSharp;
using Spectre.Console.Cli;

namespace Scrapelr.NET.Cli;

/// <summary>
/// Crawls a website and saves the collected pages as a single PDF.
/// </summary>
public sealed class CrawlCommand : AsyncCommand<CrawlSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CrawlSettings settings, CancellationToken cancellationToken)
    {
        string outputFile = Path.GetFullPath(settings.OutputFile!);

        await using IBrowser browser = await BrowserFactory.Create();
        await Pipeline.Run(browser, settings.Root!, outputFile, settings.ToCrawlOptions());

        Console.WriteLine($"Saved {outputFile}");
        return 0;
    }
}
