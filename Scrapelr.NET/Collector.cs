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
        
        /* Concurrency: If you plan to make this multithreaded, swap Queue<T> for ConcurrentQueue<T>
           and HashSet<T> for ConcurrentDictionary<T, byte>. However, I don't think the link collection
           process will be concurrent or multithreaded. */
        HashSet<Node> urlSet = new HashSet<Node>(new Comparer());
        Queue<Node> urls = new();
        var orderCounter = 0;
        
        Node leftPart = LeftPart(uri, 0, orderCounter++);
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
            
            (HashSet<Node> scrapedUris, int newOrder) = await GetLinks(browser, node, writer, urlSet, orderCounter);
            orderCounter = newOrder;
            
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
    /// <param name="orderCounter"></param>
    /// <param name="domainOnly"></param>
    /// <param name="pathOnly"></param>
    /// <returns></returns>
    private static async Task<(HashSet<Node>, int)> GetLinks(IBrowser browser, Node uri, ChannelWriter<Node> writer,
        HashSet<Node> urlSet, int orderCounter, bool domainOnly = true, bool pathOnly = true)
    {
        await using IPage? page = await browser.NewPageAsync();
        
        var root = new Uri(LeftPart(uri.Url, uri.Depth, uri.Order).Url);
        
        await page.GoToAsync(root.AbsoluteUri);
        
        const string jsSelectAllAnchors = @"Array.from(document.querySelectorAll('a')).map(a => a.href);";
        string[]? urls = await page.EvaluateExpressionAsync<string[]>(jsSelectAllAnchors);

        if (urls == null) return (new HashSet<Node>(new Comparer()), orderCounter);
        
        bool followPaths = pathOnly;
        bool followDomain = pathOnly || domainOnly; // Because pathOnly implies domainOnly

        int depth = urlSet.FirstOrDefault().Depth + 1;
        (HashSet<Node> distinctUrls, int newOrder) = DistinctUris(urls, depth, orderCounter, root, followPaths, followDomain);

        // Add unique elements of both to distinctNodes
        distinctUrls.SymmetricExceptWith(urlSet);

        await writer.WriteAllAsync(distinctUrls);

        return (distinctUrls, newOrder);
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
    /// <param name="order"></param>
    /// <returns>The parsed left part of the given URL.</returns>
    private static Node LeftPart(string url, int depth, int order)
    {
        Uri uri = new(url);
        return new Node(uri.GetLeftPart(UriPartial.Path), depth, order); 
    }

    /// <summary>
    /// Creates a set of distinct URLs from the given array by normalizing the URLs without fragments and query strings.
    /// If <paramref name="root"/> is populated, only URLs with that base path are included.
    /// </summary>
    /// <param name="urls">A string array of URLs to normalize.</param>
    /// <param name="depth">An integer counting how many links in this chain have been followed from the starting page.</param>
    /// <param name="orderCounter"></param>
    /// <param name="root">If populated, results are limited to URLs that match this base.</param>
    /// <param name="pathOnly"></param>
    /// <param name="domainOnly"></param>
    /// <returns>A HashSet of deduplicated and normalized URLs, limited to those that match the <paramref name="root"/>, if populated.</returns>
    private static (HashSet<Node>, int) DistinctUris(string[] urls, int depth, int orderCounter, Uri? root = null, bool pathOnly = true, bool domainOnly = true)
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
            
            // TODO: Figure out whether "root is null" is necessary
            
            /* TODO: Figure out why IsInDomain behaves the way it does. Passing in:
               https://learn.microsoft.com/en-us/dotnet/core/containers/overview 
               will include 
               https://learn.microsoft.com/en-us/dotnet/core/containers/sdk-publish
               https://learn.microsoft.com/en-us/dotnet/core/containers/publish-configuration 
               
               but it should only include /overview and subpaths of /overview.
               
               SOLVED: It's because the if statement below currently uses IsInDomain. Changing it to IsSubPath only includes
               the /overview page because there are no subpages of /overview. 
               TODO: Implement the logic to choose path vs domain
               */
            if (root is null || !IsInDomain(root, new Uri(normalized))) continue;
            
            Node node = new(normalized, depth, orderCounter++);
            distinctNodes.Add(node);
        }
        return (distinctNodes, orderCounter);
    }
    #endregion
}
