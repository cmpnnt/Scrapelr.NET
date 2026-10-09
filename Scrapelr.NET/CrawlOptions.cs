namespace Scrapelr.NET;

/// <summary>
/// Configuration for a single crawl. These values are constant for the lifetime of a
/// <see cref="Collector"/>, so they are supplied once at construction rather than per call.
/// </summary>
public sealed record CrawlOptions
{
    /// <summary>
    /// How many links deep to follow from the starting page. The starting page is depth 0.
    /// </summary>
    public int MaxDepth { get; init; } = 1;

    /// <summary>
    /// When true, only links sharing the starting URL's scheme, host and port are followed.
    /// </summary>
    public bool DomainOnly { get; init; } = true;

    /// <summary>
    /// When true, only links beneath the starting URL's path are followed. Implies <see cref="DomainOnly"/>.
    /// </summary>
    public bool PathOnly { get; init; } = true;

    /// <summary>
    /// Whether links must stay within the starting URL's domain, accounting for
    /// <see cref="PathOnly"/> implying <see cref="DomainOnly"/>.
    /// </summary>
    public bool FollowDomain => PathOnly || DomainOnly;
}