using Scrapelr.NET.Cli;
using Scrapelr.NET.Tests.Helpers;

namespace Scrapelr.NET.Tests.Integration;

/// <summary>
/// Crawls a local site to Markdown with a real headless browser.
/// </summary>
[Category("Integration")]
[NotInParallel("browser")]
public class MarkdownEndToEndTests
{
    private static readonly Dictionary<string, string> Pages = new()
    {
        ["/docs/"] = Page("Docs", "<p>Start with <a href=\"/docs/a\">A</a> or <a href=\"b\">B</a>.</p>"),
        ["/docs/a"] = Page("A", "<p>Page A.</p>"),
        ["/docs/b"] = Page("B", "<p>Page B.</p>"),
        ["/article"] = Page("Containers overview | Example Docs", """
            <nav><a href="/">Home</a> | <a href="/docs/">Docs</a></nav>
            <main><article>
            <h1>Containers overview</h1>
            <p>Containers package an <strong>app</strong> with its dependencies. See <a href="/docs/a">page A</a> for
            more detail on how images are layered, cached and shared between hosts, and why that makes deployments
            repeatable across machines.</p>
            <p>A second paragraph describes registries, tags and how a runtime pulls an image before starting a
            container, giving reader view plenty of text to treat this element as the page's main content.</p>
            <pre><code>docker build -t app .</code></pre>
            </article></main>
            <footer>Copyright Example Corp</footer>
            """, h1: false),
        ["/spa"] = Page("Rendered by script", """
            <div id="app"></div>
            <script>
              document.getElementById("app").innerHTML = "<h1>Rendered by script</h1><p>Built in the browser.</p>";
            </script>
            """, h1: false)
    };

    private static string Page(string title, string body, bool h1 = true) =>
        $"<html><head><title>{title}</title></head><body>{(h1 ? $"<h1>{title}</h1>" : "")}{body}</body></html>";

    private static async Task<string> Crawl(TestSite site, TempDirectory temp, params string[] args)
    {
        string output = temp.File("site.md");
        int exitCode = await CrawlCommand.Create().Parse(["-o", output, ..args]).InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
        return await File.ReadAllTextAsync(output);
    }

    [Test]
    public async Task MarkdownOutput_HasOneSectionPerPageInCrawlOrder()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();

        string markdown = await Crawl(site, temp, "-r", site.Url("/docs/"));

        string[] headings = markdown.Split('\n').Where(line => line.StartsWith("# ")).ToArray();
        await Assert.That(headings).IsEquivalentTo(["# Docs", "# A", "# B"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(markdown).Contains($"Source: <{site.Url("/docs/a")}>");
        await Assert.That(markdown.Split("\n---\n").Length).IsEqualTo(3);
    }

    [Test]
    public async Task MarkdownOutput_MakesLinksAbsolute()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();

        string markdown = await Crawl(site, temp, "-r", site.Url("/docs/"), "-m", "0");

        await Assert.That(markdown).Contains($"[A]({site.Url("/docs/a")})");
        await Assert.That(markdown).Contains($"[B]({site.Url("/docs/b")})");
    }

    [Test]
    public async Task MarkdownOutput_KeepsMainContentOnly()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();

        string markdown = await Crawl(site, temp, "-r", site.Url("/article"), "-m", "0");

        await Assert.That(markdown).StartsWith("# Containers overview\n");
        await Assert.That(markdown).Contains("Containers package an **app** with its dependencies.");
        await Assert.That(markdown).Contains("```\ndocker build -t app .\n```");
        await Assert.That(markdown).DoesNotContain("Copyright Example Corp");
        await Assert.That(markdown).DoesNotContain("[Home]");
    }

    [Test]
    public async Task MarkdownOutput_FullPage_KeepsPageChrome()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();

        string markdown = await Crawl(site, temp, "-r", site.Url("/article"), "-m", "0", "--fullPage");

        await Assert.That(markdown).Contains("Copyright Example Corp");
        await Assert.That(markdown).Contains($"[Home]({site.BaseUrl})");
    }

    [Test]
    public async Task MarkdownOutput_IncludesContentRenderedByJavaScript()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();

        string markdown = await Crawl(site, temp, "-r", site.Url("/spa"), "-m", "0", "--fullPage");

        await Assert.That(markdown).StartsWith("# Rendered by script\n");
        await Assert.That(markdown).Contains("Built in the browser.");
        await Assert.That(markdown).DoesNotContain("innerHTML");
    }

    [Test]
    public async Task MarkdownOutput_FromInputFile_FollowsFileOrder()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();
        string urls = temp.File("urls.txt");
        await File.WriteAllLinesAsync(urls, [site.Url("/docs/b"), site.Url("/docs/a")]);

        string markdown = await Crawl(site, temp, "-i", urls);

        await Assert.That(markdown.IndexOf("# B\n", StringComparison.Ordinal))
            .IsLessThan(markdown.IndexOf("# A\n", StringComparison.Ordinal));
    }

    [Test]
    public async Task Executable_MarkdownExtension_WritesMarkdownFile()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();

        (int exitCode, _) = await AppRunner.Run(temp.FullName, "-r", site.Url("/docs/"), "-o", "out/site.md", "-m", "0");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(await File.ReadAllTextAsync(temp.File("out", "site.md"))).StartsWith("# Docs\n");
    }

    [Test]
    public async Task Executable_FormatFlag_OverridesExtension()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();

        (int exitCode, _) = await AppRunner.Run(temp.FullName, "-r", site.Url("/docs/"), "-o", "site.txt", "-f", "markdown", "-m", "0");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(await File.ReadAllTextAsync(temp.File("site.txt"))).StartsWith("# Docs\n");
    }

    [Test]
    public async Task Executable_PdfFormatFlag_WritesPdfDespiteMarkdownExtension()
    {
        using var site = new TestSite(Pages);
        using var temp = new TempDirectory();

        (int exitCode, _) = await AppRunner.Run(temp.FullName, "-r", site.Url("/docs/"), "-o", "site.md", "-f", "pdf", "-m", "0");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(TestPdf.PageCount(temp.File("site.md"))).IsEqualTo(1);
    }
}
