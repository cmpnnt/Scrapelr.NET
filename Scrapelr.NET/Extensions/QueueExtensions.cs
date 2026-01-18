using Scrapelr.NET.Link;

namespace Scrapelr.NET.Extensions;

public static class QueueExtensions
{
    public static void EnqueueRange(this Queue<Node> queue, ISet<Node> items, HashSet<Node> set)
    {
        foreach (Node item in items)
        {
            if (!set.Add(item)) continue;
            queue.Enqueue(item);
        }
    }
}
