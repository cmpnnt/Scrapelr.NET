using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET.Extensions;
using Scrapelr.NET.Link;

namespace Scrapelr.NET;

/// <summary>
/// Crawls a website from one or more starting URLs, writing collected links to a ChannelWriter to be
/// read by a processor and saved as PDF files.
/// </summary>
/// <remarks>
/// One instance represents one crawl: the visited set, the pending queue and the ordering counter are
/// all per-crawl state held as fields. Call one of the <c>Collect</c> overloads once per instance.
/// </remarks>
public class Collector : ICollector
{
    private readonly IBrowser _browser;
    private readonly ChannelWriter<Node> _writer;
    private readonly CrawlOptions _options;

    /* Concurrency: If you plan to make this multithreaded, swap Queue<T> for ConcurrentQueue<T>
       and HashSet<T> for ConcurrentDictionary<T, byte>. However, I don't think the link collection
       process will be concurrent or multithreaded. */
    private readonly HashSet<Node> _urlSet = new(new Comparer());
    private readonly Queue<Node> _urls = new();
    private int _orderCounter;
    private bool _collected;

    // The starting URL currently being crawled. Followed links are filtered against it, not against the
    // page they were found on.
    private Uri _root = null!;

    /// <summary>
    /// Creates a collector for a single crawl.
    /// </summary>
    /// <param name="browser">The browser used to load pages. Its lifetime is owned by the caller.</param>
    /// <param name="writer">The channel the collected links are published to.</param>
    /// <param name="options">Crawl configuration. Defaults are used when omitted.</param>
    public Collector(IBrowser browser, ChannelWriter<Node> writer, CrawlOptions? options = null)
    {
        _browser = browser;
        _writer = writer;
        _options = options ?? new CrawlOptions();
    }

    /// <summary>
    /// Given a starting URL, crawls a website according to the configured <see cref="CrawlOptions"/>,
    /// writing collected links to the ChannelWriter and completing it when the crawl finishes. If the
    /// crawl fails, the writer is completed with the exception so readers don't wait forever.
    /// </summary>
    /// <param name="uri">The URL to start crawling from.</param>
    /// <exception cref="InvalidOperationException">Thrown when the instance has already been used.</exception>
    public Task Collect(string uri) => Collect([uri]);

    /// <summary>
    /// Crawls from each starting URL in turn, as <see cref="Collect(string)"/> does for one. Each
    /// starting URL is published in the order given, followed by the links collected from it, and links
    /// are filtered against the starting URL they were reached from.
    /// </summary>
    /// <param name="uris">The URLs to start crawling from.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="uris"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the instance has already been used.</exception>
    public async Task Collect(IEnumerable<string> uris)
    {
        ArgumentNullException.ThrowIfNull(uris);
        List<string> starts = uris.ToList();
        if (starts.Count == 0)
        {
            throw new ArgumentException("At least one URL is required.", nameof(uris));
        }

        if (_collected)
        {
            throw new InvalidOperationException($"{nameof(Collect)} may only be called once per {nameof(Collector)}.");
        }
        _collected = true;

        try
        {
            await Crawl(starts);
            _writer.Complete();
        }
        catch (Exception ex)
        {
            _writer.TryComplete(ex);
            throw;
        }
    }

    #region private
    /// <summary>
    /// Crawls from each of <paramref name="uris"/> in turn, skipping duplicates.
    /// </summary>
    /// <param name="uris">The URLs to start crawling from.</param>
    private async Task Crawl(List<string> uris)
    {
        // Every starting URL is marked as seen up front, so one found while crawling from an earlier
        // starting URL is published in its own place rather than early and at the wrong depth.
        List<Uri> roots = [];
        foreach (string uri in uris)
        {
            Node start = LeftPart(uri, 0, 0);
            if (_urlSet.Add(start))
            {
                roots.Add(new Uri(start.Url));
            }
        }

        foreach (Uri root in roots)
        {
            await CrawlFrom(root);
        }
    }

    /// <summary>
    /// Crawls breadth-first from <paramref name="root"/>, publishing it and each newly discovered link.
    /// </summary>
    /// <param name="root">The normalized URL to start crawling from.</param>
    private async Task CrawlFrom(Uri root)
    {
        _root = root;
        var start = new Node(root.AbsoluteUri, 0, _orderCounter++);
        _urls.Enqueue(start);

        await _writer.WriteAsync(start);

        while (_urls.Count > 0)
        {
            Node node = _urls.Dequeue();

            // Links found on a page are one level deeper than it, so a page at MaxDepth is collected
            // but not scraped for links.
            if (node.Depth >= _options.MaxDepth)
            {
                continue;
            }

            HashSet<Node> scrapedUris = await GetLinks(node);

            _urls.EnqueueRange(scrapedUris, _urlSet);
        }
    }

    /// <summary>
    /// Loads a single page, extracts its anchors and publishes the ones not already seen.
    /// </summary>
    /// <param name="node">The page to scrape.</param>
    /// <returns>The newly discovered links, to be queued for crawling.</returns>
    private async Task<HashSet<Node>> GetLinks(Node node)
    {
        await using IPage? page = await _browser.NewPageAsync();

        await page.GoToAsync(node.Url);

        const string jsSelectAllAnchors = @"Array.from(document.querySelectorAll('a')).map(a => a.href);";
        string[]? urls = await page.EvaluateExpressionAsync<string[]>(jsSelectAllAnchors);

        if (urls == null) return new HashSet<Node>(new Comparer());

        HashSet<Node> distinctUrls = DistinctUris(urls, node.Depth + 1, _root);

        // Drop links that have already been collected
        distinctUrls.ExceptWith(_urlSet);

        await _writer.WriteAllAsync(distinctUrls);

        return distinctUrls;
    }

    /// <summary>
    /// Creates a set of distinct URLs from the given array by normalizing the URLs without fragments and query strings.
    /// Results are limited to URLs matching <paramref name="root"/> according to the configured
    /// <see cref="CrawlOptions"/>.
    /// </summary>
    /// <param name="urls">A string array of URLs to normalize.</param>
    /// <param name="depth">An integer counting how many links in this chain have been followed from the starting page.</param>
    /// <param name="root">Results are limited to URLs that match this base.</param>
    /// <returns>A HashSet of deduplicated and normalized URLs.</returns>
    private HashSet<Node> DistinctUris(string[] urls, int depth, Uri root)
    {
        var distinctNodes = new HashSet<Node>(new Comparer());

        // Remove duplicate URLs by normalizing URLs without fragments and query strings
        foreach (string s in urls)
        {
            if (!Uri.TryCreate(s, UriKind.Absolute, out Uri? url)) continue;

            // Skip mailto:, javascript: and the like, which can't be rendered as pages
            if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps) continue;

            string normalized = url.GetLeftPart(UriPartial.Path);

            if (_options.FollowDomain && !IsInDomain(root, new Uri(normalized))) continue;
            if (_options.PathOnly && !IsSubPath(root, new Uri(normalized))) continue;

            Node node = new(normalized, depth, _orderCounter++);
            distinctNodes.Add(node);
        }
        return distinctNodes;
    }

    private static bool IsInDomain(Uri parentUri, Uri childUri)
    {
        if (!parentUri.IsAbsoluteUri || !childUri.IsAbsoluteUri)
        {
            return false;
        }

        return parentUri.Scheme == childUri.Scheme &&
               parentUri.Host == childUri.Host &&
               parentUri.Port == childUri.Port;
    }

    /// <summary>
    /// Determines whether a <paramref name="childUri"/> is a sub path of a <paramref name="parentUri"/>.
    /// </summary>
    /// <param name="parentUri">The URI to be compared against.</param>
    /// <param name="childUri">The URI to be compared.</param>
    /// <returns>True if <paramref name="childUri"/> is a sub path of <paramref name="parentUri"/>.</returns>
    private static bool IsSubPath(Uri parentUri, Uri childUri)
    {
        if (!IsInDomain(parentUri, childUri))
        {
            return false;
        }

        string parentPath = parentUri.AbsolutePath;
        string normalizedParent = parentPath.EndsWith('/') ? parentPath : parentPath + "/";
        string childPath = childUri.AbsolutePath;
        string normalizedChild = childPath.EndsWith('/') ? childPath : childPath + "/";

        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Given a string representation of a URL, returns the
    /// <see href="https://learn.microsoft.com/en-us/dotnet/api/System.Uri.GetLeftPart?view=net-10.0">left part</see>.
    /// </summary>
    /// <param name="url">The URL whose left part to return.</param>
    /// <param name="depth">The number of the current link in the crawl chain.</param>
    /// <param name="order">The position of this link in the parsed page ordering.</param>
    /// <returns>The parsed left part of the given URL.</returns>
    private static Node LeftPart(string url, int depth, int order)
    {
        Uri uri = new(url);
        return new Node(uri.GetLeftPart(UriPartial.Path), depth, order);
    }

    #endregion
}
