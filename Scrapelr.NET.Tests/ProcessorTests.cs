using System.Threading.Channels;
using NSubstitute;
using PuppeteerSharp;
using Scrapelr.NET.Link;
using Scrapelr.NET.Tests.Helpers;
using TUnit.Assertions.Enums;

namespace Scrapelr.NET.Tests;

public class ProcessorTests
{
    private const string A = "https://example.com/a";
    private const string B = "https://example.com/b";
    private const string C = "https://example.com/c";

    private static ChannelReader<Node> Links(params Node[] nodes)
    {
        var channel = Channel.CreateUnbounded<Node>();
        foreach (Node node in nodes)
        {
            channel.Writer.TryWrite(node);
        }
        channel.Writer.Complete();
        return channel.Reader;
    }

    [Test]
    public async Task Process_MergesPagesByNodeOrder_RegardlessOfArrivalOrCompletionOrder()
    {
        using var temp = new TempDirectory();
        string output = temp.File("out.pdf");
        FakeBrowser browser = new FakeBrowser()
            .WithWidth(A, 100).WithWidth(B, 110).WithWidth(C, 120)
            .WithDelay(A, TimeSpan.FromMilliseconds(150));

        await new Processor(browser.Browser, Links(new Node(C, 1, 2), new Node(A, 0, 0), new Node(B, 1, 1))).Process(output);

        await Assert.That(TestPdf.PageWidths(output)).IsEquivalentTo([100d, 110d, 120d], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Process_OrdersNumericallyNotLexically()
    {
        using var temp = new TempDirectory();
        string output = temp.File("out.pdf");
        FakeBrowser browser = new FakeBrowser().WithWidth(A, 100).WithWidth(B, 110);

        await new Processor(browser.Browser, Links(new Node(B, 1, 10), new Node(A, 1, 9))).Process(output);

        await Assert.That(TestPdf.PageWidths(output)).IsEquivalentTo([100d, 110d], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Process_SkipsBlankUrls()
    {
        using var temp = new TempDirectory();
        string output = temp.File("out.pdf");
        var browser = new FakeBrowser();

        await new Processor(browser.Browser, Links(new Node("", 0, 0), new Node("  ", 0, 1), new Node(A, 0, 2))).Process(output);

        await Assert.That(browser.Visited).IsEquivalentTo([A]);
        await Assert.That(TestPdf.PageCount(output)).IsEqualTo(1);
    }

    [Test]
    public async Task Process_SkipsPagesThatFailToLoad()
    {
        using var temp = new TempDirectory();
        string output = temp.File("out.pdf");
        FakeBrowser browser = new FakeBrowser()
            .WithWidth(A, 100).WithWidth(C, 120)
            .WithFailure(B);

        await new Processor(browser.Browser, Links(new Node(A, 0, 0), new Node(B, 1, 1), new Node(C, 1, 2))).Process(output);

        await Assert.That(TestPdf.PageWidths(output)).IsEquivalentTo([100d, 120d], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Process_SkipsPagesThatTimeOut()
    {
        using var temp = new TempDirectory();
        string output = temp.File("out.pdf");
        FakeBrowser browser = new FakeBrowser().WithFailure(B, new TimeoutException());

        await new Processor(browser.Browser, Links(new Node(A, 0, 0), new Node(B, 1, 1))).Process(output);

        await Assert.That(TestPdf.PageCount(output)).IsEqualTo(1);
    }

    [Test]
    public async Task Process_UnexpectedError_PropagatesAndCleansUp()
    {
        using var temp = new TempDirectory();
        string output = temp.File("out.pdf");
        FakeBrowser browser = new FakeBrowser().WithFailure(B, new IOException("disk full"));

        await Assert.That(async () => await new Processor(browser.Browser, Links(new Node(A, 0, 0), new Node(B, 1, 1))).Process(output))
            .Throws<IOException>();

        await Assert.That(File.Exists(output)).IsFalse();
        await Assert.That(Directory.Exists(Path.GetDirectoryName(browser.PdfPaths.Single()))).IsFalse();
    }

    [Test]
    public async Task Process_NoPageRenders_ThrowsAndWritesNothing()
    {
        using var temp = new TempDirectory();
        string output = temp.File("out.pdf");
        FakeBrowser browser = new FakeBrowser().WithFailure(A);

        await Assert.That(async () => await new Processor(browser.Browser, Links(new Node(A, 0, 0))).Process(output))
            .Throws<InvalidOperationException>();

        await Assert.That(File.Exists(output)).IsFalse();
    }

    [Test]
    public async Task Process_EmptyChannel_Throws()
    {
        using var temp = new TempDirectory();

        await Assert.That(async () => await new Processor(new FakeBrowser().Browser, Links()).Process(temp.File("out.pdf")))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Process_DeletesIntermediatePdfs()
    {
        using var temp = new TempDirectory();
        var browser = new FakeBrowser();

        await new Processor(browser.Browser, Links(new Node(A, 0, 0), new Node(B, 1, 1))).Process(temp.File("out.pdf"));

        await Assert.That(browser.PdfPaths.Count).IsEqualTo(2);
        foreach (string path in browser.PdfPaths)
        {
            await Assert.That(File.Exists(path)).IsFalse();
            await Assert.That(Directory.Exists(Path.GetDirectoryName(path))).IsFalse();
        }
    }

    [Test]
    public async Task Process_DisposesEveryPage()
    {
        using var temp = new TempDirectory();
        FakeBrowser browser = new FakeBrowser().WithFailure(B);

        await new Processor(browser.Browser, Links(new Node(A, 0, 0), new Node(B, 1, 1))).Process(temp.File("out.pdf"));

        await Assert.That(browser.Pages.Count).IsEqualTo(2);
        foreach (IPage page in browser.Pages)
        {
            await page.Received(1).DisposeAsync();
        }
    }

    [Test]
    [Arguments(1)]
    [Arguments(3)]
    public async Task Process_LimitsConcurrentPages(int maxConcurrency)
    {
        using var temp = new TempDirectory();
        string output = temp.File("out.pdf");
        var browser = new FakeBrowser();
        Node[] nodes = Enumerable.Range(0, 10).Select(i => new Node($"https://example.com/{i}", 1, i)).ToArray();
        foreach (Node node in nodes)
        {
            browser.WithDelay(node.Url, TimeSpan.FromMilliseconds(20));
        }

        await new Processor(browser.Browser, Links(nodes), maxConcurrency).Process(output);

        await Assert.That(browser.MaxOpenPages).IsLessThanOrEqualTo(maxConcurrency);
        await Assert.That(TestPdf.PageCount(output)).IsEqualTo(10);
    }

    [Test]
    public async Task Process_FaultedChannel_Throws()
    {
        using var temp = new TempDirectory();
        var channel = Channel.CreateUnbounded<Node>();
        channel.Writer.Complete(new NavigationException("crawl failed"));

        await Assert.That(async () => await new Processor(new FakeBrowser().Browser, channel.Reader).Process(temp.File("out.pdf")))
            .Throws<NavigationException>();
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Process_BlankOutputFile_Throws(string outputFile)
    {
        await Assert.That(async () => await new Processor(new FakeBrowser().Browser, Links()).Process(outputFile))
            .Throws<ArgumentException>();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task Constructor_NonPositiveConcurrency_Throws(int maxConcurrency)
    {
        await Assert.That(() => new Processor(new FakeBrowser().Browser, Links(), maxConcurrency))
            .Throws<ArgumentOutOfRangeException>();
    }
}
