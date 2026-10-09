using System.CommandLine;
using System.CommandLine.Parsing;
using PuppeteerSharp;

namespace Scrapelr.NET.Cli;

/// <summary>
/// Builds the command line for crawling a website and saving the collected pages as a single PDF.
/// </summary>
public static class CrawlCommand
{
    /// <summary>
    /// Creates the command, which crawls with a real browser when invoked.
    /// </summary>
    public static RootCommand Create() => Create(Execute);

    /// <summary>
    /// Creates the command, handing validated settings to <paramref name="run"/> when invoked.
    /// </summary>
    /// <param name="run">Runs the crawl and returns the exit code.</param>
    public static RootCommand Create(Func<CrawlSettings, CancellationToken, Task<int>> run)
    {
        var root = new Option<string>("--root", "-r")
        {
            Description = "The URL to start crawling from. Required unless --inputFile is given."
        };
        var inputFile = new Option<string>("--inputFile", "-i")
        {
            Description = "A text file of URLs to start crawling from, one per line. Blank lines and lines " +
                          "starting with # are ignored. Use instead of --root."
        };
        var outputFile = new Option<string>("--outputFile", "-o")
        {
            Description = "Where to save the merged PDF. Existing files are overwritten.",
            Required = true
        };
        var maxDepth = new Option<int?>("--maxDepth", "-m")
        {
            Description = "How many links deep to follow from each starting page, which is depth 0. " +
                          "[default: 1, or 0 with --inputFile]"
        };
        var pathOnly = new Option<bool>("--pathOnly", "-p")
        {
            Description = "Only follow links beneath the starting URL's path. Implies --domainOnly.",
            DefaultValueFactory = _ => true
        };
        var domainOnly = new Option<bool>("--domainOnly", "-d")
        {
            Description = "Only follow links on the starting URL's scheme, host and port.",
            DefaultValueFactory = _ => true
        };

        var command = new RootCommand("Crawls a website and saves the collected pages as a single PDF.")
        {
            root, inputFile, outputFile, maxDepth, pathOnly, domainOnly
        };

        CrawlSettings Bind(CommandResult result) => new()
        {
            Root = result.GetValue(root),
            InputFile = result.GetValue(inputFile),
            OutputFile = result.GetValue(outputFile),
            MaxDepth = result.GetValue(maxDepth),
            PathOnly = result.GetValue(pathOnly),
            DomainOnly = result.GetValue(domainOnly)
        };

        // Validated here rather than in command.Validators, where omitted options read as default(T)
        // instead of their DefaultValueFactory value and unparsable values throw.
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            CrawlSettings settings = Bind(parseResult.CommandResult);

            if (settings.Validate() is { } error)
            {
                await parseResult.InvocationConfiguration.Error.WriteLineAsync(error);
                return 1;
            }

            return await run(settings, cancellationToken);
        });

        return command;
    }

    private static async Task<int> Execute(CrawlSettings settings, CancellationToken cancellationToken)
    {
        string outputFile = Path.GetFullPath(settings.OutputFile!);

        await using IBrowser browser = await BrowserFactory.Create();
        await Pipeline.Create(browser, settings.ToCrawlOptions()).Run(settings.GetStartUrls(), outputFile);

        Console.WriteLine($"Saved {outputFile}");
        return 0;
    }
}
