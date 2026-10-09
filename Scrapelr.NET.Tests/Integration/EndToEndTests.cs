using System.Diagnostics;
using PuppeteerSharp;
using Scrapelr.NET.Cli;
using Scrapelr.NET.Tests.Helpers;

namespace Scrapelr.NET.Tests.Integration;

/// <summary>
/// Crawls a local site with a real headless browser. The first run downloads Chromium.
/// </summary>
[Category("Integration")]
[NotInParallel("browser")]
public class EndToEndTests
{
    private static readonly Dictionary<string, string> Pages = new()
    {
        ["/docs/"] = Html("Docs", "/docs/a", "/docs/b#section", "/other/", "https://example.invalid/"),
        ["/docs/a"] = Html("A", "/docs/a/deep", "/docs/"),
        ["/docs/b"] = Html("B", "/docs/a"),
        ["/docs/a/deep"] = Html("Deep"),
        ["/other/"] = Html("Other")
    };

    private static string Html(string title, params string[] links) =>
        $"<html><head><title>{title}</title></head><body><h1>{title}</h1>" +
        string.Concat(links.Select(href => $"<a href=\"{href}\">{href}</a> ")) +
        "</body></html>";

    [Test]
    public async Task BrowserFactory_LaunchesAConnectedBrowser()
    {
        await using IBrowser browser = await BrowserFactory.Create();

        await Assert.That(browser.IsConnected).IsTrue();
    }

    [Test]
    public async Task CrawlCommand_WritesOnePdfToOutputFile()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();
        string output = temp.File("nested", "site.pdf");

        int exitCode = await CrawlCommand.Create().Parse(["-r", site.Url("/docs/"), "-o", output]).InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
        // The root page and its two in-path links
        await Assert.That(TestPdf.PageCount(output)).IsEqualTo(3);
    }

    [Test]
    [Arguments("-m 0", 1)]
    [Arguments("-m 2", 4)]
    [Arguments("-p false", 4)]
    public async Task CrawlCommand_AppliesCrawlOptions(string options, int expectedPages)
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();
        string output = temp.File("site.pdf");

        int exitCode = await CrawlCommand.Create()
            .Parse(["-r", site.Url("/docs/"), "-o", output, ..options.Split(' ')])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(TestPdf.PageCount(output)).IsEqualTo(expectedPages);
    }

    [Test]
    public async Task CrawlCommand_InputFile_WritesOnlyListedPagesByDefault()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();
        string urls = temp.File("urls.txt");
        string output = temp.File("pages.pdf");
        await File.WriteAllLinesAsync(urls, ["# Two pages", site.Url("/other/"), "", site.Url("/docs/b")]);

        int exitCode = await CrawlCommand.Create().Parse(["-i", urls, "-o", output]).InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(TestPdf.PageCount(output)).IsEqualTo(2);
    }

    [Test]
    public async Task CrawlCommand_InputFileWithMaxDepth_FollowsLinksFromEachUrl()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();
        string urls = temp.File("urls.txt");
        string output = temp.File("pages.pdf");
        await File.WriteAllLinesAsync(urls, [site.Url("/docs/"), site.Url("/other/")]);

        int exitCode = await CrawlCommand.Create().Parse(["-i", urls, "-o", output, "-m", "1"]).InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
        // /docs/ and its two in-path links, then /other/
        await Assert.That(TestPdf.PageCount(output)).IsEqualTo(4);
    }

    [Test]
    public async Task Executable_RelativeInputFile_IsReadFromWorkingDirectory()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();
        await File.WriteAllLinesAsync(temp.File("urls.txt"), [site.Url("/docs/a/deep")]);

        (int exitCode, _) = await RunApp(temp.FullName, "-i", "urls.txt", "-o", "deep.pdf");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(TestPdf.PageCount(temp.File("deep.pdf"))).IsEqualTo(1);
    }

    [Test]
    public async Task Executable_RelativeOutputFile_IsWrittenUnderWorkingDirectory()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();

        (int exitCode, string stdout) = await RunApp(temp.FullName, "-r", site.Url("/docs/"), "--outputFile", "out/site.pdf");

        string expected = temp.File("out", "site.pdf");
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(TestPdf.PageCount(expected)).IsEqualTo(3);
        // The temp path may be reported through a symlink (/var vs /private/var on macOS)
        string saved = stdout.Split('\n').Single(line => line.StartsWith("Saved ")).Trim();
        await Assert.That(saved).EndsWith(Path.Combine(Path.GetFileName(temp.FullName), "out", "site.pdf"));
    }

    [Test]
    public async Task Executable_OverwritesExistingOutputFile()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();
        string output = temp.File("site.pdf");
        await File.WriteAllTextAsync(output, "stale");

        (int exitCode, _) = await RunApp(temp.FullName, "-r", site.Url("/docs/"), "-o", output, "-m", "0");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(TestPdf.PageCount(output)).IsEqualTo(1);
    }

    [Test]
    public async Task Executable_WithoutOutputFile_FailsWithoutCrawling()
    {
        using var temp = new TempDirectory();

        (int exitCode, string stdout) = await RunApp(temp.FullName, "-r", "http://127.0.0.1:1/");

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(stdout).Contains("Option '--outputFile' is required.");
        await Assert.That(Directory.EnumerateFileSystemEntries(temp.FullName)).IsEmpty();
    }

    [Test]
    public async Task Executable_UnreachableRoot_FailsAndWritesNothing()
    {
        using var temp = new TempDirectory();
        string output = temp.File("site.pdf");

        (int exitCode, _) = await RunApp(temp.FullName, "-r", "http://127.0.0.1:1/", "-o", output);

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(File.Exists(output)).IsFalse();
    }

    /// <summary>
    /// Runs the built Scrapelr.NET executable, which the project reference copies next to the tests.
    /// </summary>
    private static async Task<(int ExitCode, string Stdout)> RunApp(string workingDirectory, params string[] args)
    {
        string app = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Scrapelr.NET.exe" : "Scrapelr.NET");
        var startInfo = new ProcessStartInfo(app, args)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = Process.Start(startInfo)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await stdout + await stderr);
    }
}
