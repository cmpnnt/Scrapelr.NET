using PuppeteerSharp;
using Scrapelr.NET.Tests.Helpers;
using TUnit.Assertions.Enums;

namespace Scrapelr.NET.Tests;

public class PipelineTests
{
    private const string Root = "https://example.com/docs/";
    private const string A = "https://example.com/docs/a";
    private const string B = "https://example.com/docs/b";
    private const string Deep = "https://example.com/docs/a/deep";

    private static FakeBrowser Site() => new FakeBrowser()
        .WithPage(Root, A, B).WithWidth(Root, 100)
        .WithPage(A, Deep, Root).WithWidth(A, 110)
        .WithPage(B, A).WithWidth(B, 120)
        .WithWidth(Deep, 130);

    [Test]
    public async Task Run_WritesOnePdfOfEveryCrawledPageInCrawlOrder()
    {
        using var temp = new TempDirectory();
        string output = temp.File("site.pdf");
        // Slow the first page down so renders finish out of crawl order.
        FakeBrowser browser = Site().WithDelay(Root, TimeSpan.FromMilliseconds(100));

        await Pipeline.Run(browser.Browser, Root, output, new CrawlOptions { MaxDepth = 2 });

        await Assert.That(TestPdf.PageWidths(output)).IsEquivalentTo([100d, 110d, 120d, 130d], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Run_AppliesCrawlOptions()
    {
        using var temp = new TempDirectory();
        string output = temp.File("site.pdf");

        await Pipeline.Run(Site().Browser, Root, output, new CrawlOptions { MaxDepth = 0 });

        await Assert.That(TestPdf.PageWidths(output)).IsEquivalentTo([100d]);
    }

    [Test]
    public async Task Run_CrawlFailure_PropagatesAndWritesNothing()
    {
        using var temp = new TempDirectory();
        string output = temp.File("site.pdf");
        FakeBrowser browser = Site().WithFailure(Root);

        await Assert.That(async () => await Pipeline.Run(browser.Browser, Root, output, new CrawlOptions()))
            .Throws<NavigationException>();

        await Assert.That(File.Exists(output)).IsFalse();
    }
}
