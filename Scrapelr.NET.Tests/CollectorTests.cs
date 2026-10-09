using System.Threading.Channels;
using NSubstitute;
using PuppeteerSharp;
using Scrapelr.NET.Link;
using Scrapelr.NET.Tests.Helpers;
using TUnit.Assertions.Enums;

namespace Scrapelr.NET.Tests;

public class CollectorTests
{
    private const string Root = "https://example.com/docs/";
    private const string A = "https://example.com/docs/a";
    private const string B = "https://example.com/docs/b";
    private const string AChild = "https://example.com/docs/a/child";
    private const string Sibling = "https://example.com/docs/sibling";
    private const string Grandchild = "https://example.com/docs/a/child/grandchild";
    private const string OffPath = "https://example.com/blog/post";
    private const string OffPathFromA = "https://example.com/blog/from-a";
    private const string OtherHost = "https://other.example/docs/x";
    private const string OtherScheme = "http://example.com/docs/insecure";
    private const string OtherPort = "https://example.com:8443/docs/port";

    private static FakeBrowser Site() => new FakeBrowser()
        .WithPage(Root,
            A, B + "#section", A + "?tab=1", "https://EXAMPLE.com/docs/A",
            OffPath, OtherHost, OtherScheme, OtherPort,
            "mailto:someone@example.com", "javascript:void(0)", "not a url", "")
        .WithPage(A, AChild, Sibling, OffPathFromA, Root)
        .WithPage(B, A, B)
        .WithPage(AChild, Grandchild);

    private static async Task<List<Node>> Crawl(FakeBrowser browser, string start = Root, CrawlOptions? options = null)
    {
        var channel = Channel.CreateUnbounded<Node>();
        await new Collector(browser.Browser, channel.Writer, options).Collect(start);
        return await channel.Reader.ReadAllAsync().ToListAsync();
    }

    private static IEnumerable<string> Urls(IEnumerable<Node> nodes) => nodes.Select(n => n.Url);

    [Test]
    public async Task Collect_PublishesNormalizedStartUrlFirst()
    {
        List<Node> nodes = await Crawl(Site(), Root + "?query=1#top");

        await Assert.That(nodes[0]).IsEqualTo(new Node(Root, 0, 0));
    }

    [Test]
    public async Task Collect_MaxDepthZero_PublishesOnlyStartPageWithoutLoadingIt()
    {
        FakeBrowser browser = Site();

        List<Node> nodes = await Crawl(browser, options: new CrawlOptions { MaxDepth = 0 });

        await Assert.That(Urls(nodes)).IsEquivalentTo([Root]);
        await Assert.That(browser.Visited.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Collect_MaxDepthOne_PublishesStartPageAndItsLinks()
    {
        List<Node> nodes = await Crawl(Site(), options: new CrawlOptions { MaxDepth = 1 });

        await Assert.That(Urls(nodes)).IsEquivalentTo([Root, A, B], CollectionOrdering.Matching);
        await Assert.That(nodes.Select(n => n.Depth)).IsEquivalentTo([0, 1, 1], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Collect_MaxDepthOne_OnlyLoadsTheStartPage()
    {
        FakeBrowser browser = Site();

        await Crawl(browser, options: new CrawlOptions { MaxDepth = 1 });

        await Assert.That(browser.Visited).IsEquivalentTo([Root]);
    }

    [Test]
    public async Task Collect_MaxDepthTwo_FollowsLinksOfLinks()
    {
        List<Node> nodes = await Crawl(Site(), options: new CrawlOptions { MaxDepth = 2 });

        await Assert.That(Urls(nodes)).IsEquivalentTo([Root, A, B, AChild, Sibling]);
        await Assert.That(nodes.Single(n => n.Url == AChild).Depth).IsEqualTo(2);
        await Assert.That(Urls(nodes)).DoesNotContain(Grandchild);
    }

    [Test]
    public async Task Collect_PublishesEachUrlOnce()
    {
        FakeBrowser browser = Site();

        List<Node> nodes = await Crawl(browser, options: new CrawlOptions { MaxDepth = 5 });

        string[] urls = Urls(nodes).ToArray();
        await Assert.That(urls.Distinct(StringComparer.OrdinalIgnoreCase).Count()).IsEqualTo(urls.Length);
        await Assert.That(browser.Visited.Distinct().Count()).IsEqualTo(browser.Visited.Count);
    }

    [Test]
    public async Task Collect_PathOnly_FiltersAgainstStartUrlRatherThanCurrentPage()
    {
        // Sibling is beneath the start URL but not beneath A, the page it was found on.
        List<Node> nodes = await Crawl(Site(), options: new CrawlOptions { MaxDepth = 2, PathOnly = true });

        await Assert.That(Urls(nodes)).Contains(Sibling);
        await Assert.That(Urls(nodes)).DoesNotContain(OffPath);
        await Assert.That(Urls(nodes)).DoesNotContain(OffPathFromA);
    }

    [Test]
    public async Task Collect_DomainOnly_FollowsOtherPathsOnSameOrigin()
    {
        List<Node> nodes = await Crawl(Site(), options: new CrawlOptions { MaxDepth = 2, PathOnly = false, DomainOnly = true });

        await Assert.That(Urls(nodes)).Contains(OffPath);
        await Assert.That(Urls(nodes)).Contains(OffPathFromA);
        await Assert.That(Urls(nodes)).DoesNotContain(OtherHost);
        await Assert.That(Urls(nodes)).DoesNotContain(OtherScheme);
        await Assert.That(Urls(nodes)).DoesNotContain(OtherPort);
    }

    [Test]
    public async Task Collect_Unrestricted_FollowsOtherOriginsButOnlyHttpLinks()
    {
        List<Node> nodes = await Crawl(Site(), options: new CrawlOptions { MaxDepth = 1, PathOnly = false, DomainOnly = false });

        await Assert.That(Urls(nodes)).IsEquivalentTo([Root, A, B, OffPath, OtherHost, OtherScheme, OtherPort]);
    }

    [Test]
    public async Task Collect_AssignsUniqueIncreasingOrderInPublicationOrder()
    {
        List<Node> nodes = await Crawl(Site(), options: new CrawlOptions { MaxDepth = 3 });

        int[] orders = nodes.Select(n => n.Order).ToArray();
        await Assert.That(orders).IsEquivalentTo(orders.Order(), CollectionOrdering.Matching);
        await Assert.That(orders.Distinct().Count()).IsEqualTo(orders.Length);
    }

    [Test]
    public async Task Collect_CompletesTheChannel()
    {
        var channel = Channel.CreateUnbounded<Node>();

        await new Collector(Site().Browser, channel.Writer).Collect(Root);

        // Completion only finishes once every published item has been read
        await channel.Reader.ReadAllAsync().ToListAsync();
        await Assert.That(channel.Reader.Completion.IsCompletedSuccessfully).IsTrue();
    }

    [Test]
    public async Task Collect_DisposesEveryPage()
    {
        FakeBrowser browser = Site();

        await Crawl(browser, options: new CrawlOptions { MaxDepth = 3 });

        await Assert.That(browser.Pages.Count).IsGreaterThan(0);
        foreach (IPage page in browser.Pages)
        {
            await page.Received(1).DisposeAsync();
        }
    }

    [Test]
    public async Task Collect_PageReturningNoLinks_EndsTheCrawl()
    {
        FakeBrowser browser = new FakeBrowser().WithPage(Root, null);

        List<Node> nodes = await Crawl(browser);

        await Assert.That(Urls(nodes)).IsEquivalentTo([Root]);
    }

    [Test]
    public async Task Collect_DefaultOptions_FollowOneLevelWithinPath()
    {
        var channel = Channel.CreateUnbounded<Node>();

        await new Collector(Site().Browser, channel.Writer).Collect(Root);

        List<Node> nodes = await channel.Reader.ReadAllAsync().ToListAsync();
        await Assert.That(Urls(nodes)).IsEquivalentTo([Root, A, B]);
    }

    [Test]
    public async Task Collect_CalledTwice_Throws()
    {
        var channel = Channel.CreateUnbounded<Node>();
        var collector = new Collector(Site().Browser, channel.Writer);
        await collector.Collect(Root);

        await Assert.That(async () => await collector.Collect(Root)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Collect_PageFailure_FaultsTheChannelSoReadersStop()
    {
        var failure = new NavigationException("net::ERR_NAME_NOT_RESOLVED");
        FakeBrowser browser = Site().WithFailure(Root, failure);
        var channel = Channel.CreateUnbounded<Node>();

        await Assert.That(async () => await new Collector(browser.Browser, channel.Writer).Collect(Root))
            .Throws<NavigationException>();

        await Assert.That(async () => await channel.Reader.ReadAllAsync().ToListAsync()).Throws<NavigationException>();
    }

    [Test]
    public async Task Collect_InvalidStartUrl_FaultsTheChannel()
    {
        var channel = Channel.CreateUnbounded<Node>();

        await Assert.That(async () => await new Collector(Site().Browser, channel.Writer).Collect("not a url"))
            .Throws<UriFormatException>();

        await Assert.That(channel.Reader.Completion.IsFaulted).IsTrue();
    }

    private static async Task<List<Node>> CrawlMany(FakeBrowser browser, IEnumerable<string> starts, CrawlOptions? options = null)
    {
        var channel = Channel.CreateUnbounded<Node>();
        await new Collector(browser.Browser, channel.Writer, options).Collect(starts);
        return await channel.Reader.ReadAllAsync().ToListAsync();
    }

    [Test]
    public async Task CollectMany_MaxDepthZero_PublishesStartUrlsInGivenOrder()
    {
        FakeBrowser browser = Site();

        List<Node> nodes = await CrawlMany(browser, [B, Root, A], new CrawlOptions { MaxDepth = 0 });

        await Assert.That(Urls(nodes)).IsEquivalentTo([B, Root, A], CollectionOrdering.Matching);
        await Assert.That(nodes.Select(n => n.Depth)).IsEquivalentTo([0, 0, 0]);
        await Assert.That(browser.Visited.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CollectMany_DeduplicatesNormalizedStartUrls()
    {
        List<Node> nodes = await CrawlMany(Site(), [A, A + "#top", "https://EXAMPLE.com/docs/a?x=1", B],
            new CrawlOptions { MaxDepth = 0 });

        await Assert.That(Urls(nodes)).IsEquivalentTo([A, B], CollectionOrdering.Matching);
    }

    [Test]
    public async Task CollectMany_GroupsEachStartUrlWithItsLinks()
    {
        List<Node> nodes = await CrawlMany(Site(), ["https://example.com/blog/", Root], new CrawlOptions { MaxDepth = 1 });

        // The blog page links to nothing in the fake site, so it stands alone before the docs crawl.
        await Assert.That(Urls(nodes))
            .IsEquivalentTo(["https://example.com/blog/", Root, A, B], CollectionOrdering.Matching);
    }

    [Test]
    public async Task CollectMany_FiltersLinksAgainstTheStartUrlTheyWereReachedFrom()
    {
        const string Blog = "https://example.com/blog/";
        FakeBrowser browser = Site().WithPage(Blog, OffPath, A);

        List<Node> nodes = await CrawlMany(browser, [Root, Blog], new CrawlOptions { MaxDepth = 1 });

        // OffPath is outside the docs path, so the docs crawl skips it but the blog crawl collects it.
        // A is outside the blog path, and was already collected by the docs crawl anyway.
        await Assert.That(Urls(nodes)).IsEquivalentTo([Root, A, B, Blog, OffPath], CollectionOrdering.Matching);
    }

    [Test]
    public async Task CollectMany_LaterStartUrlFoundEarlier_KeepsItsOwnPlaceAndDepth()
    {
        // A is linked from Root but listed second, so it's published second at depth 0 and crawled from there.
        List<Node> nodes = await CrawlMany(Site(), [Root, A], new CrawlOptions { MaxDepth = 1 });

        // Sibling isn't beneath A, which is now the start URL its links are filtered against.
        await Assert.That(Urls(nodes)).IsEquivalentTo([Root, B, A, AChild], CollectionOrdering.Matching);
        await Assert.That(nodes.Single(n => n.Url == A).Depth).IsEqualTo(0);
    }

    [Test]
    public async Task CollectMany_AssignsUniqueIncreasingOrderAcrossStartUrls()
    {
        List<Node> nodes = await CrawlMany(Site(), [B, Root], new CrawlOptions { MaxDepth = 2 });

        int[] orders = nodes.Select(n => n.Order).ToArray();
        await Assert.That(orders).IsEquivalentTo(orders.Order(), CollectionOrdering.Matching);
        await Assert.That(orders.Distinct().Count()).IsEqualTo(orders.Length);
    }

    [Test]
    public async Task CollectMany_NoUrls_Throws()
    {
        var channel = Channel.CreateUnbounded<Node>();

        await Assert.That(async () => await new Collector(Site().Browser, channel.Writer).Collect(Array.Empty<string>()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task CollectMany_InvalidUrl_FailsBeforePublishingAnything()
    {
        var channel = Channel.CreateUnbounded<Node>();

        await Assert.That(async () => await new Collector(Site().Browser, channel.Writer).Collect([Root, "not a url"]))
            .Throws<UriFormatException>();

        await Assert.That(channel.Reader.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CollectMany_AfterCollect_Throws()
    {
        var channel = Channel.CreateUnbounded<Node>();
        var collector = new Collector(Site().Browser, channel.Writer);
        await collector.Collect(Root);

        await Assert.That(async () => await collector.Collect([Root])).Throws<InvalidOperationException>();
    }
}
