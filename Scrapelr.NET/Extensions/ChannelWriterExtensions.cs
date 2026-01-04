using System.Threading.Channels;
using Scrapelr.NET.Link;

namespace Scrapelr.NET.Extensions;

public static class ChannelWriterExtensions
{
    /// <summary>
    /// Asynchronously writes all items from a HashSet to the ChannelWriter.
    /// </summary>
    /// <param name="writer">The ChannelWriter to write to.</param>
    /// <param name="items">The HashSet of strings to add.</param>
    /// <param name="cancellationToken">A cancellation token to observe.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    public static async ValueTask WriteAllAsync(
        this ChannelWriter<Node> writer, 
        HashSet<Node> items, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(items);

        foreach (Node item in items)
        {
            await writer.WriteAsync(item, cancellationToken);
        }
    }
}