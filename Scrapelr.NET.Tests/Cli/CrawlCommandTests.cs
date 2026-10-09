using System.CommandLine;
using Scrapelr.NET.Cli;

namespace Scrapelr.NET.Tests.Cli;

/// <summary>
/// Runs real command lines through <see cref="CrawlCommand"/> with the crawl swapped for a recorder.
/// </summary>
public class CrawlCommandTests
{
    private const string Root = "https://example.com/docs/";

    private sealed record Result(int ExitCode, CrawlSettings? Settings, string Output, string Error);

    private static async Task<Result> Invoke(params string[] args)
    {
        CrawlSettings? captured = null;
        RootCommand command = CrawlCommand.Create((settings, _) =>
        {
            captured = settings;
            return Task.FromResult(0);
        });

        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = await command.Parse(args).InvokeAsync(new InvocationConfiguration { Output = output, Error = error });

        return new Result(exitCode, captured, output.ToString(), error.ToString());
    }

    [Test]
    public async Task ShortOptions_Bind()
    {
        Result result = await Invoke("-r", Root, "-o", "out.pdf");

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.Settings!.Root).IsEqualTo(Root);
        await Assert.That(result.Settings.OutputFile).IsEqualTo("out.pdf");
    }

    [Test]
    public async Task LongOptions_Bind()
    {
        Result result = await Invoke("--root", Root, "--outputFile", "dir/site.pdf");

        await Assert.That(result.Settings!.Root).IsEqualTo(Root);
        await Assert.That(result.Settings.OutputFile).IsEqualTo("dir/site.pdf");
    }

    [Test]
    [Arguments("--outputFile=site.pdf")]
    [Arguments("--outputFile:site.pdf")]
    public async Task OutputFile_AcceptsInlineValue(string option)
    {
        Result result = await Invoke("-r", Root, option);

        await Assert.That(result.Settings!.OutputFile).IsEqualTo("site.pdf");
    }

    [Test]
    public async Task OutputFile_AcceptsPathsWithSpaces()
    {
        Result result = await Invoke("-r", Root, "-o", "My Docs/site.pdf");

        await Assert.That(result.Settings!.OutputFile).IsEqualTo("My Docs/site.pdf");
    }

    [Test]
    [Arguments("-i")]
    [Arguments("--inputFile")]
    public async Task InputFile_Binds(string option)
    {
        using var temp = new Helpers.TempDirectory();
        string urls = temp.File("urls.txt");
        await File.WriteAllLinesAsync(urls, [Root, "https://example.com/blog/"]);

        Result result = await Invoke(option, urls, "-o", "out.pdf");

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.Settings!.InputFile).IsEqualTo(urls);
        await Assert.That(result.Settings.Root).IsNull();
        await Assert.That(result.Settings.GetStartUrls()).IsEquivalentTo([Root, "https://example.com/blog/"]);
    }

    [Test]
    public async Task InputFile_WithoutMaxDepth_CollectsOnlyListedPages()
    {
        using var temp = new Helpers.TempDirectory();
        string urls = temp.File("urls.txt");
        await File.WriteAllLinesAsync(urls, [Root]);

        Result result = await Invoke("-i", urls, "-o", "out.pdf");

        await Assert.That(result.Settings!.MaxDepth).IsNull();
        await Assert.That(result.Settings.ToCrawlOptions().MaxDepth).IsEqualTo(0);
    }

    [Test]
    public async Task InputFile_WithMaxDepth_FollowsLinks()
    {
        using var temp = new Helpers.TempDirectory();
        string urls = temp.File("urls.txt");
        await File.WriteAllLinesAsync(urls, [Root]);

        Result result = await Invoke("-i", urls, "-o", "out.pdf", "-m", "2");

        await Assert.That(result.Settings!.ToCrawlOptions().MaxDepth).IsEqualTo(2);
    }

    [Test]
    public async Task OmittedOptions_UseDefaults()
    {
        Result result = await Invoke("-r", Root, "-o", "out.pdf");

        await Assert.That(result.Settings!.ToCrawlOptions()).IsEqualTo(new CrawlOptions());
    }

    [Test]
    public async Task CrawlOptions_Bind()
    {
        Result result = await Invoke("-r", Root, "-o", "out.pdf", "-m", "3", "-p", "false", "-d", "false");

        await Assert.That(result.Settings!.ToCrawlOptions())
            .IsEqualTo(new CrawlOptions { MaxDepth = 3, PathOnly = false, DomainOnly = false });
    }

    [Test]
    public async Task DomainOnly_CanBeDisabledWithPathOnly()
    {
        Result result = await Invoke("-r", Root, "-o", "out.pdf", "--pathOnly", "false", "--domainOnly", "false");

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.Settings!.DomainOnly).IsFalse();
    }

    [Test]
    public async Task BareBooleanFlag_MeansTrue()
    {
        Result result = await Invoke("-r", Root, "-o", "out.pdf", "--pathOnly", "--domainOnly");

        await Assert.That(result.Settings!.PathOnly).IsTrue();
        await Assert.That(result.Settings.DomainOnly).IsTrue();
    }

    [Test]
    public async Task Run_ExitCodeIsReturned()
    {
        RootCommand command = CrawlCommand.Create((_, _) => Task.FromResult(7));

        int exitCode = await command.Parse(["-r", Root, "-o", "out.pdf"]).InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(7);
    }

    [Test]
    public async Task Help_ListsEveryOption()
    {
        Result result = await Invoke("--help");

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.Settings).IsNull();
        foreach (string option in new[] { "--root", "--inputFile", "--outputFile", "--maxDepth", "--pathOnly", "--domainOnly" })
        {
            await Assert.That(result.Output).Contains(option);
        }
    }

    [Test]
    [Arguments($"-r {Root}", "Option '--outputFile' is required.")]
    [Arguments("-o out.pdf", "Specify --root or --inputFile.")]
    [Arguments($"-r {Root} -i urls.txt -o out.pdf", "Specify --root or --inputFile, not both.")]
    [Arguments("-i missing-urls.txt -o out.pdf", "--inputFile not found: missing-urls.txt")]
    [Arguments($"-r {Root} -o", "Required argument missing for option: '-o'.")]
    [Arguments($"-r {Root} -o out.pdf -m lots", "Cannot parse argument 'lots' for option '--maxDepth'")]
    [Arguments($"-r {Root} -o out.pdf -p nope", "Unrecognized command or argument 'nope'.")]
    [Arguments($"-r {Root} -o out.pdf -m -1", "--maxDepth can't be negative.")]
    [Arguments($"-r {Root} -o out.pdf -d false", "--domainOnly can't be false while --pathOnly is true.")]
    [Arguments("-r ftp://example.com/ -o out.pdf", "--root must be an absolute http or https URL")]
    public async Task InvalidCommandLine_ReportsErrorWithoutRunning(string commandLine, string expectedError)
    {
        Result result = await Invoke(commandLine.Split(' '));

        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.Settings).IsNull();
        await Assert.That(result.Error).Contains(expectedError);
    }
}
