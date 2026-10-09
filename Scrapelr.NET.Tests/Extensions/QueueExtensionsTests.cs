using Scrapelr.NET.Extensions;
using Scrapelr.NET.Link;

namespace Scrapelr.NET.Tests.Extensions;

public class QueueExtensionsTests
{
    [Test]
    public async Task EnqueueRange_EnqueuesOnlyItemsNotAlreadySeen()
    {
        var seen = new HashSet<Node>(new Comparer()) { new("https://example.com/a", 0, 0) };
        var queue = new Queue<Node>();
        var items = new HashSet<Node>(new Comparer())
        {
            new("https://example.com/a", 1, 1),
            new("https://example.com/b", 1, 2)
        };

        queue.EnqueueRange(items, seen);

        await Assert.That(queue.Select(n => n.Url)).IsEquivalentTo(["https://example.com/b"]);
    }

    [Test]
    public async Task EnqueueRange_AddsEnqueuedItemsToSeenSet()
    {
        var seen = new HashSet<Node>(new Comparer());
        var queue = new Queue<Node>();
        var items = new HashSet<Node>(new Comparer()) { new("https://example.com/a", 1, 1) };

        queue.EnqueueRange(items, seen);
        queue.EnqueueRange(items, seen);

        await Assert.That(queue.Count).IsEqualTo(1);
        await Assert.That(seen.Contains(new Node("https://example.com/a", 0, 0))).IsTrue();
    }

    [Test]
    public async Task EnqueueRange_WithNoItems_LeavesQueueEmpty()
    {
        var queue = new Queue<Node>();

        queue.EnqueueRange(new HashSet<Node>(), new HashSet<Node>(new Comparer()));

        await Assert.That(queue.Count).IsEqualTo(0);
    }
}
