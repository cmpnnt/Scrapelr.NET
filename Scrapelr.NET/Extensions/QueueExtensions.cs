namespace Scrapelr.NET.Extensions;

public static class QueueExtensions
{
    public static void EnqueueRange<T>(this Queue<T> queue, T[] items, HashSet<T> set)
    {
        foreach (T item in items)
        {
            if (!set.Add(item)) continue;
            queue.Enqueue(item);
        }
    }
}
