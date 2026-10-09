using Scrapelr.NET.Cli;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Scrapelr.NET.Tests.Cli;

/// <summary>
/// Runs real command lines through Spectre to check how they bind to <see cref="CrawlSettings"/>.
/// </summary>
[NotInParallel(nameof(CrawlSettingsParsingTests))]
public class CrawlSettingsParsingTests
{
    private const string Root = "https://example.com/docs/";

    private static async Task<(int ExitCode, CrawlSettings? Settings)> Parse(params string[] args)
    {
        CaptureCommand.Captured = null;

        var app = new CommandApp<CaptureCommand>();
        app.Configure(config => config.ConfigureConsole(
            AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(TextWriter.Null) })));

        int exitCode = await app.RunAsync(args);
        return (exitCode, CaptureCommand.Captured);
    }

    [Test]
    public async Task ShortOptions_Bind()
    {
        (int exitCode, CrawlSettings? settings) = await Parse("-r", Root, "-o", "out.pdf");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(settings!.Root).IsEqualTo(Root);
        await Assert.That(settings.OutputFile).IsEqualTo("out.pdf");
    }

    [Test]
    public async Task LongOptions_Bind()
    {
        (_, CrawlSettings? settings) = await Parse("--root", Root, "--outputFile", "dir/site.pdf");

        await Assert.That(settings!.Root).IsEqualTo(Root);
        await Assert.That(settings.OutputFile).IsEqualTo("dir/site.pdf");
    }

    [Test]
    public async Task OutputFile_AcceptsEqualsSyntax()
    {
        (_, CrawlSettings? settings) = await Parse("-r", Root, "--outputFile=site.pdf");

        await Assert.That(settings!.OutputFile).IsEqualTo("site.pdf");
    }

    [Test]
    public async Task OutputFile_AcceptsPathsWithSpaces()
    {
        (_, CrawlSettings? settings) = await Parse("-r", Root, "-o", "My Docs/site.pdf");

        await Assert.That(settings!.OutputFile).IsEqualTo("My Docs/site.pdf");
    }

    [Test]
    public async Task OmittedOptions_UseDefaults()
    {
        (_, CrawlSettings? settings) = await Parse("-r", Root, "-o", "out.pdf");

        await Assert.That(settings!.ToCrawlOptions()).IsEqualTo(new CrawlOptions());
    }

    [Test]
    public async Task CrawlOptions_Bind()
    {
        (_, CrawlSettings? settings) = await Parse("-r", Root, "-o", "out.pdf", "-m", "3", "-p", "false", "-d", "false");

        await Assert.That(settings!.ToCrawlOptions()).IsEqualTo(new CrawlOptions { MaxDepth = 3, PathOnly = false, DomainOnly = false });
    }

    [Test]
    public async Task BareBooleanFlag_MeansTrue()
    {
        (_, CrawlSettings? settings) = await Parse("-r", Root, "-o", "out.pdf", "--pathOnly", "--domainOnly");

        await Assert.That(settings!.PathOnly).IsTrue();
        await Assert.That(settings.DomainOnly).IsTrue();
    }

    [Test]
    [Arguments($"-r {Root}")]
    [Arguments($"-r {Root} -o")]
    [Arguments("-o out.pdf")]
    [Arguments($"-r {Root} -o out.pdf -d false")]
    [Arguments($"-r {Root} -o out.pdf -m -1")]
    [Arguments($"-r {Root} -o out.pdf -m lots")]
    public async Task InvalidCommandLine_FailsWithoutRunning(string commandLine)
    {
        (int exitCode, CrawlSettings? settings) = await Parse(commandLine.Split(' '));

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(settings).IsNull();
    }
}

/// <summary>
/// Records the settings it was run with instead of crawling.
/// </summary>
public sealed class CaptureCommand : Command<CrawlSettings>
{
    public static CrawlSettings? Captured { get; set; }

    public override int Execute(CommandContext context, CrawlSettings settings, CancellationToken cancellationToken)
    {
        Captured = settings;
        return 0;
    }
}
