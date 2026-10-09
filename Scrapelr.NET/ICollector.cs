namespace Scrapelr.NET;

/// <summary>
/// Crawls a website from a starting URL, publishing the links it finds for a
/// <see cref="IProcessor"/> to consume.
/// </summary>
public interface ICollector
{
    /// <summary>
    /// Crawls from <paramref name="uri"/> and writes every collected link to the channel.
    /// </summary>
    /// <param name="uri">The URL to start crawling from.</param>
    Task Collect(string uri);
}