using System.Threading.Channels;
using Scrapelr.NET.Extensions;
using Scrapelr.NET.Link;

namespace Scrapelr.NET.Tests.Extensions;

public class ChannelWriterExtensionsTests
{
    [Test]
    public async Task WriteAllAsync_WritesEveryItem()
    {
        var channel = Channel.CreateUnbounded<Node>();
        var items = new HashSet<Node>(new Comparer())
        {
            new("https://example.com/a", 1, 1),
            new("https://example.com/b", 1, 2)
        };

        await channel.Writer.WriteAllAsync(items);
        channel.Writer.Complete();

        List<Node> read = await channel.Reader.ReadAllAsync().ToListAsync();
        await Assert.That(read).IsEquivalentTo(items);
    }

    [Test]
    public async Task WriteAllAsync_NullWriter_Throws()
    {
        ChannelWriter<Node> writer = null!;

        await Assert.That(async () => await writer.WriteAllAsync([])).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task WriteAllAsync_NullItems_Throws()
    {
        var channel = Channel.CreateUnbounded<Node>();

        await Assert.That(async () => await channel.Writer.WriteAllAsync(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task WriteAllAsync_HonoursCancellation()
    {
        var channel = Channel.CreateBounded<Node>(1);
        await channel.Writer.WriteAsync(new Node("https://example.com/full", 0, 0));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var items = new HashSet<Node>(new Comparer()) { new("https://example.com/a", 1, 1) };

        await Assert.That(async () => await channel.Writer.WriteAllAsync(items, cts.Token))
            .Throws<OperationCanceledException>();
    }
}
