using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET.Extensions;
using Scrapelr.NET.Link;

namespace Scrapelr.NET;

public class Collector
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="browser"></param>
    /// <param name="uri"></param>
    /// <param name="writer"></param>
    /// <param name="maxDepth"></param>
    public static async Task Collect(IBrowser browser, string uri, ChannelWriter<Node> writer, int maxDepth = 3)
    {
        
        // TODO: Implement the `domainOnly` logic to stay inside the current domain
        /*  TODO: Implement the `pathOnly` logic to only pick up child paths of the starting URL.
            It's currently all screwed up. */
        
        // Concurrency: If you plan to make this multithreaded, swap Queue<T> for ConcurrentQueue<T>
        // and HashSet<T> for ConcurrentDictionary<T, byte>.
        HashSet<Node> urlSet = new HashSet<Node>(new Comparer());
        Queue<Node> urls = new();
        
        Node leftPart = LeftPart(uri, 0);
        urls.Enqueue(leftPart);
        urlSet.Add(leftPart);
        
        await writer.WriteAsync(leftPart);
        
        while (urls.Count > 0)
        {
            Node node = urls.Dequeue();
            if (node.Depth > maxDepth)
            {
                continue;
            }
            
            HashSet<Node> scrapedUris = await GetLinks(browser, node, writer, urlSet);
            urls.EnqueueRange(scrapedUris, urlSet);
        }
        writer.Complete();
    }

    #region private

    /// <summary>
    /// 
    /// </summary>
    /// <param name="browser"></param>
    /// <param name="uri"></param>
    /// <param name="writer"></param>
    /// <param name="urlSet"></param>
    /// <param name="domainOnly"></param>
    /// <param name="pathOnly"></param>
    /// <returns></returns>
    private static async Task<HashSet<Node>> GetLinks(IBrowser browser, Node uri, ChannelWriter<Node> writer, 
        HashSet<Node> urlSet, bool domainOnly = true, bool pathOnly = true)
    {
        await using IPage? page = await browser.NewPageAsync();
        
        var root = new Uri(LeftPart(uri.Url, 0).Url);
        
        await page.SetRequestInterceptionAsync(true);
        page.Request += async (_, e) =>
        {
            // Ensure valid URI
            if (!Uri.TryCreate(e.Request.Url, UriKind.Absolute, out Uri? requestUri))
            {
                await e.Request.AbortAsync(); 
                return;
            }

            // Only allow requests that are children of the root
            // This blocks external content, which we don't need to load to scrape links
            if (IsSubPath(new Uri(root.Scheme + "://" + root.Host), requestUri))
            {
                await e.Request.ContinueAsync();
            }
            else
            {
                await e.Request.AbortAsync();
            }
        };
        
        await page.GoToAsync(root.AbsoluteUri);
        
        const string jsSelectAllAnchors = @"Array.from(document.querySelectorAll('a')).map(a => a.href);";
        string[]? urls = await page.EvaluateExpressionAsync<string[]>(jsSelectAllAnchors);

        if (urls == null) return [];
        
        // Normalized and deduplicate the links on the page being crawled.
        HashSet<Node> distinctUrls;
        if (pathOnly)
        {
            distinctUrls = DistinctUris(urls, urlSet.FirstOrDefault().Depth + 1, root, true, true);
        }
        else if (domainOnly)
        {
            distinctUrls = DistinctUris(urls, urlSet.FirstOrDefault().Depth + 1, root, false, true);
        }
        else
        {
            distinctUrls = DistinctUris(urls, urlSet.FirstOrDefault().Depth + 1, root, false, false);
        }
        
        // Add unique elements of both to distinctNodes
        distinctUrls.SymmetricExceptWith(urlSet);
        
        await writer.WriteAllAsync(distinctUrls);

        return distinctUrls;
    }

    /// <summary>
    /// Downloads a headless Chromium browser engine for using PuppeteerSharp.
    /// </summary>
    /// <returns>A PuppeteerSharp IBrowser instance.</returns>
    public static async Task<IBrowser> Setup()
    {
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();
        IBrowser? browser = await Puppeteer.LaunchAsync(
            new LaunchOptions { Headless = true }
        );
        
        return browser;
    }

    /// <summary>
    /// Determines whether a <paramref name="childUri"/> is in the same domain as <paramref name="parentUri"/>.
    /// </summary>
    /// <param name="parentUri">The URI to be compared against.</param>
    /// <param name="childUri">The URI to be compared.</param>
    /// <returns>True if <paramref name="childUri"/> is a sub path of <paramref name="parentUri"/>.</returns>
    private static bool IsInDomain(Uri parentUri, Uri childUri)
    {
        if (!parentUri.IsAbsoluteUri || !childUri.IsAbsoluteUri)
        {
            return false;
        }
        
        return parentUri.IsBaseOf(childUri);
    }
    
    /// <summary>
    /// Determines whether a <paramref name="childUri"/> is a sub path of a <paramref name="parentUri"/>.
    /// </summary>
    /// <param name="parentUri">The URI to be compared against.</param>
    /// <param name="childUri">The URI to be compared.</param>
    /// <returns>True if <paramref name="childUri"/> is a sub path of <paramref name="parentUri"/>.</returns>
    private static bool IsSubPath(Uri parentUri, Uri childUri)
    {
        if (!parentUri.IsAbsoluteUri || !childUri.IsAbsoluteUri)
            return false;
        
        if (parentUri.Scheme != childUri.Scheme ||
            parentUri.Host != childUri.Host ||
            parentUri.Port != childUri.Port)
        {
            return false;
        }
        
        string parentPath = parentUri.AbsolutePath;
        string normalizedParent = parentPath.EndsWith("/") ? parentPath : parentPath + "/";
        string childPath = childUri.AbsolutePath;
        
        return childPath.Equals(parentPath, StringComparison.OrdinalIgnoreCase) ||
               childPath.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Given a string representation of a URL, returns the
    /// <see href="https://learn.microsoft.com/en-us/dotnet/api/System.Uri.GetLeftPart?view=net-10.0">left part</see>.
    /// </summary>
    /// <param name="url">The URL whose left part to return.</param>
    /// <param name="depth">The number of the current link in the crawl chain.</param>
    /// <returns>The parsed left part of the given URL.</returns>
    private static Node LeftPart(string url, int depth)
    {
        Uri uri = new(url);
        return new Node(uri.GetLeftPart(UriPartial.Path), depth); 
    }

    /// <summary>
    /// Creates a set of distinct URLs from the given array by normalizing the URLs without fragments and query strings.
    /// If <paramref name="root"/> is populated, only URLs with that base path are included.
    /// </summary>
    /// <param name="urls">A string array of URLs to normalize.</param>
    /// <param name="depth">An integer counting how many links in this chain have been followed from the starting page.</param>
    /// <param name="root">If populated, results are limited to URLs that match this base.</param>
    /// <param name="pathOnly"></param>
    /// <param name="domainOnly"></param>
    /// <returns>A HashSet of deduplicated and normalized URLs, limited to those that match the <paramref name="root"/>, if populated.</returns>
    private static HashSet<Node> DistinctUris(string[] urls, int depth, Uri? root = null, bool pathOnly = true, bool domainOnly = true)
    {
        var distinctNodes = new HashSet<Node>(new Comparer());

        // Remove duplicate URLs by normalizing URLs without fragments and query strings
        foreach (string s in urls)
        {
            if (!Uri.TryCreate(s, UriKind.Absolute, out Uri? url)) continue;
            
            string normalized = url.GetLeftPart(UriPartial.Path);

            // When root is populated, only add subpaths of the root
            /* TODO: The `domainOnly` and `pathOnly` logic needs to be updated here with some Uri parsing based on
               the values of domainOnly and pathOnly passed in here. See: IsSubPath and IsInDomain.
               Note: pathOnly implies domainOnly, and domainOnly can't be false if pathOnly is true. Similarly,
               pathOnly takes precedence over domainOnly. This should be minimally handled here to prevent crashes
               and should be explicitly rejected in the command line if the user provides incorrect args. */
            if (root is null || !IsSubPath(root, new Uri(normalized))) continue;
            
            Node node = new(normalized, depth);
            distinctNodes.Add(node);
        }
        return distinctNodes;
    }

    /// <summary>
    /// Creates a HashSet of Node structs from a HashSet of strings representing URLs.
    /// </summary>
    /// <param name="urls">An array of strings representing URLs.</param>
    /// <param name="depth">An integer counting how many links in this chain have been followed from the starting page.</param>
    /// <returns>A HashSet of Nodes from the url string array.</returns>
    private static HashSet<Node> CreateNodes(HashSet<string> urls, int depth)
    {
        var nodes = new HashSet<Node>(new Comparer());
        foreach (string url in urls)
        {
            Node node = new(url, depth);
            nodes.Add(node);
        }
        return nodes;
    }
    #endregion
}
