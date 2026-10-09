using System.Threading.Channels;
using NSubstitute;
using PuppeteerSharp;
using Scrapelr.NET.Link;
using Scrapelr.NET.Tests.Helpers;
using TUnit.Assertions.Enums;

namespace Scrapelr.NET.Tests;

public class PipelineTests
{
    private const string Root = "https://example.com/docs/";
    private const string A = "https://example.com/docs/a";
    private const string B = "https://example.com/docs/b";
    private const string Deep = "https://example.com/docs/a/deep";
    private const string Blog = "https://example.com/blog/";
    private const string BlogPost = "https://example.com/blog/post";

    private static FakeBrowser Site() => new FakeBrowser()
        .WithPage(Root, A, B, BlogPost).WithWidth(Root, 100)
        .WithPage(A, Deep, Root).WithWidth(A, 110)
        .WithPage(B, A).WithWidth(B, 120)
        .WithWidth(Deep, 130)
        .WithPage(Blog, BlogPost).WithWidth(Blog, 200)
        .WithWidth(BlogPost, 210);

    [Test]
    public async Task Run_WritesOnePdfOfEveryCrawledPageInCrawlOrder()
    {
        using var temp = new TempDirectory();
        string output = temp.File("site.pdf");
        // Slow the first page down so renders finish out of crawl order.
        FakeBrowser browser = Site().WithDelay(Root, TimeSpan.FromMilliseconds(100));

        await Pipeline.Create(browser.Browser, new CrawlOptions { MaxDepth = 2 }).Run([Root], output);

        await Assert.That(TestPdf.PageWidths(output)).IsEquivalentTo([100d, 110d, 120d, 130d], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Run_AppliesCrawlOptions()
    {
        using var temp = new TempDirectory();
        string output = temp.File("site.pdf");

        await Pipeline.Create(Site().Browser, new CrawlOptions { MaxDepth = 0 }).Run([Root], output);

        await Assert.That(TestPdf.PageWidths(output)).IsEquivalentTo([100d]);
    }

    [Test]
    public async Task Run_SeveralStartUrls_GroupsPagesByStartUrl()
    {
        using var temp = new TempDirectory();
        string output = temp.File("site.pdf");

        await Pipeline.Create(Site().Browser, new CrawlOptions { MaxDepth = 1 }).Run([Blog, Root], output);

        await Assert.That(TestPdf.PageWidths(output))
            .IsEquivalentTo([200d, 210d, 100d, 110d, 120d], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Run_CrawlFailure_PropagatesAndWritesNothing()
    {
        using var temp = new TempDirectory();
        string output = temp.File("site.pdf");
        FakeBrowser browser = Site().WithFailure(Root);

        await Assert.That(async () => await Pipeline.Create(browser.Browser, new CrawlOptions()).Run([Root], output))
            .Throws<NavigationException>();

        await Assert.That(File.Exists(output)).IsFalse();
    }

    [Test]
    public async Task Run_ConnectsInjectedCollectorToInjectedProcessor()
    {
        var published = new Node(Root, 0, 0);
        List<Node> received = [];
        string? processedOutput = null;
        IEnumerable<string>? collectedUris = null;

        ICollector collector = null!;
        var pipeline = new Pipeline(
            writer =>
            {
                collector = Substitute.For<ICollector>();
                collector.Collect(Arg.Any<IEnumerable<string>>()).Returns(async ci =>
                {
                    collectedUris = ci.Arg<IEnumerable<string>>();
                    await writer.WriteAsync(published);
                    writer.Complete();
                });
                return collector;
            },
            reader =>
            {
                IProcessor processor = Substitute.For<IProcessor>();
                processor.Process(Arg.Any<string>()).Returns(async ci =>
                {
                    processedOutput = ci.Arg<string>();
                    await foreach (Node node in reader.ReadAllAsync())
                    {
                        received.Add(node);
                    }
                });
                return processor;
            });

        await pipeline.Run([Root, Blog], "out.pdf");

        await Assert.That(collectedUris).IsEquivalentTo([Root, Blog], CollectionOrdering.Matching);
        await Assert.That(received).IsEquivalentTo([published]);
        await Assert.That(processedOutput).IsEqualTo("out.pdf");
    }

    [Test]
    public async Task Run_ProcessorFailure_Propagates()
    {
        var pipeline = new Pipeline(
            writer =>
            {
                ICollector collector = Substitute.For<ICollector>();
                collector.Collect(Arg.Any<IEnumerable<string>>()).Returns(_ =>
                {
                    writer.Complete();
                    return Task.CompletedTask;
                });
                return collector;
            },
            _ =>
            {
                IProcessor processor = Substitute.For<IProcessor>();
                processor.Process(Arg.Any<string>()).Returns(Task.FromException(new IOException("disk full")));
                return processor;
            });

        await Assert.That(async () => await pipeline.Run([Root], "out.pdf")).Throws<IOException>();
    }

    [Test]
    public async Task Constructor_NullFactory_Throws()
    {
        await Assert.That(() => new Pipeline(null!, _ => Substitute.For<IProcessor>())).Throws<ArgumentNullException>();
        await Assert.That(() => new Pipeline(_ => Substitute.For<ICollector>(), null!)).Throws<ArgumentNullException>();
    }
}
