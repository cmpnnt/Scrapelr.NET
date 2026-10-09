using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET.Link;
using Scrapelr.NET.Tests.Helpers;

namespace Scrapelr.NET.Tests;

public class MarkdownOutputFormatTests
{
    private const string Url = "https://example.com/docs/containers";

    private static async Task<IPage> LoadedPage(FakeBrowser browser, string url)
    {
        IPage page = await browser.Browser.NewPageAsync();
        await page.GoToAsync(url);
        return page;
    }

    [Test]
    public async Task FileExtension_IsMd()
    {
        await Assert.That(new MarkdownOutputFormat().FileExtension).IsEqualTo(".md");
    }

    [Test]
    public async Task Render_WritesConvertedPage()
    {
        using var temp = new TempDirectory();
        string path = temp.File("0.md");
        FakeBrowser browser = new FakeBrowser().WithDocument(Url, MarkdownConverterTests.ArticleHtml, "Containers overview | Docs");

        await new MarkdownOutputFormat().Render(await LoadedPage(browser, Url), new Node(Url, 0, 0), path);

        string markdown = await File.ReadAllTextAsync(path);
        await Assert.That(markdown).StartsWith($"# Containers overview\n\nSource: <{Url}>");
        await Assert.That(markdown).DoesNotContain("Copyright Example Corp");
    }

    [Test]
    public async Task Render_FullPage_KeepsPageChrome()
    {
        using var temp = new TempDirectory();
        string path = temp.File("0.md");
        FakeBrowser browser = new FakeBrowser().WithDocument(Url, MarkdownConverterTests.ArticleHtml, "Containers overview");

        await new MarkdownOutputFormat(fullPage: true).Render(await LoadedPage(browser, Url), new Node(Url, 0, 0), path);

        await Assert.That(await File.ReadAllTextAsync(path)).Contains("Copyright Example Corp");
    }

    [Test]
    public async Task Render_ResolvesLinksAgainstRedirectedUrl()
    {
        using var temp = new TempDirectory();
        string path = temp.File("0.md");
        const string moved = "https://docs.example.com/v2/containers";
        FakeBrowser browser = new FakeBrowser()
            .WithDocument(Url, "<html><body><p><a href=\"guide\">Guide</a></p></body></html>", "Moved")
            .WithRedirect(Url, moved);

        await new MarkdownOutputFormat(fullPage: true).Render(await LoadedPage(browser, Url), new Node(Url, 0, 0), path);

        string markdown = await File.ReadAllTextAsync(path);
        await Assert.That(markdown).Contains($"Source: <{moved}>");
        await Assert.That(markdown).Contains("[Guide](https://docs.example.com/v2/guide)");
    }

    [Test]
    public async Task Render_WritesUtf8WithoutByteOrderMark()
    {
        using var temp = new TempDirectory();
        string path = temp.File("0.md");
        FakeBrowser browser = new FakeBrowser().WithDocument(Url, "<html><body><p>Café — naïve</p></body></html>", "Ünïcode");

        await new MarkdownOutputFormat(fullPage: true).Render(await LoadedPage(browser, Url), new Node(Url, 0, 0), path);

        byte[] bytes = await File.ReadAllBytesAsync(path);
        await Assert.That(bytes[0]).IsEqualTo((byte)'#');
        await Assert.That(await File.ReadAllTextAsync(path)).Contains("Café — naïve");
    }

    [Test]
    public async Task Merge_JoinsPagesInOrderWithSeparators()
    {
        using var temp = new TempDirectory();
        string first = temp.File("0.md");
        string second = temp.File("1.md");
        string output = temp.File("out.md");
        await File.WriteAllTextAsync(first, "# One\n\nFirst.\n\n\n");
        await File.WriteAllTextAsync(second, "# Two\n\nSecond.\n");

        new MarkdownOutputFormat().Merge([second, first], output);

        await Assert.That(await File.ReadAllTextAsync(output))
            .IsEqualTo("# Two\n\nSecond.\n\n---\n\n# One\n\nFirst.\n");
    }

    [Test]
    public async Task Merge_SinglePage_HasNoSeparator()
    {
        using var temp = new TempDirectory();
        string page = temp.File("0.md");
        string output = temp.File("out.md");
        await File.WriteAllTextAsync(page, "# Only\n");

        new MarkdownOutputFormat().Merge([page], output);

        await Assert.That(await File.ReadAllTextAsync(output)).IsEqualTo("# Only\n");
    }

    [Test]
    public async Task Merge_CreatesMissingDirectoryAndOverwrites()
    {
        using var temp = new TempDirectory();
        string page = temp.File("0.md");
        string output = temp.File("a", "b", "out.md");
        await File.WriteAllTextAsync(page, "# New\n");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, "stale content that is longer than the new file");

        new MarkdownOutputFormat().Merge([page], output);

        await Assert.That(await File.ReadAllTextAsync(output)).IsEqualTo("# New\n");
    }

    [Test]
    public async Task Merge_NoPages_ThrowsAndWritesNothing()
    {
        using var temp = new TempDirectory();
        string output = temp.File("out.md");

        await Assert.That(() => new MarkdownOutputFormat().Merge([], output)).Throws<InvalidOperationException>();
        await Assert.That(File.Exists(output)).IsFalse();
    }

    [Test]
    public async Task Processor_WithMarkdownFormat_WritesSectionsInCrawlOrder()
    {
        using var temp = new TempDirectory();
        string output = temp.File("site.md");
        const string a = "https://example.com/a";
        const string b = "https://example.com/b";
        FakeBrowser browser = new FakeBrowser()
            .WithDocument(a, "<html><body><p>Alpha body.</p></body></html>", "Alpha")
            .WithDocument(b, "<html><body><p>Beta body.</p></body></html>", "Beta")
            .WithDelay(a, TimeSpan.FromMilliseconds(100));
        var channel = Channel.CreateUnbounded<Node>();
        channel.Writer.TryWrite(new Node(b, 1, 1));
        channel.Writer.TryWrite(new Node(a, 0, 0));
        channel.Writer.Complete();

        await new Processor(browser.Browser, channel.Reader, new MarkdownOutputFormat(fullPage: true)).Process(output);

        await Assert.That(await File.ReadAllTextAsync(output)).IsEqualTo(
            $"# Alpha\n\nSource: <{a}>\n\nAlpha body.\n\n---\n\n# Beta\n\nSource: <{b}>\n\nBeta body.\n");
        await Assert.That(browser.PdfPaths).IsEmpty();
    }
}
