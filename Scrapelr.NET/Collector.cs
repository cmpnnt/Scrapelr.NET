using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET.Extensions;

namespace Scrapelr.NET;

public class Collector
{
    public static async Task Collect(IBrowser browser, string uri, ChannelWriter<string?> writer,
        bool subPathsOnly = true, bool follow = true, int maxDepth = 3)
    {
        HashSet<string> urlSet = [];
        Queue<string> urls = new();
        string leftPart = LeftPart(uri);
        urls.Enqueue(leftPart);
        urlSet.Add(leftPart);
        
        /* TODO: Figure out how to calculate the max depth. This is tricky because it's exponentially
           generating links, and each set of links from each sibling page is at the same level. */
        var currentDepth = 0;

        while (follow && urls.Count > 0)
        {
            string[] scrapedUris = await GetLinks(browser, urls.Dequeue(), writer, subPathsOnly);
            urls.EnqueueRange(scrapedUris, urlSet);
        }
        writer.Complete();
    }

    #region private
    private static async Task<string[]> GetLinks(IBrowser browser, string uri, ChannelWriter<string?> writer, 
        bool subPathsOnly = true)
    {
        await using IPage? page = await browser.NewPageAsync();
        
        var root = new Uri(LeftPart(uri));
        
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
        
        HashSet<string> distinctUrls = DistinctUris(urls, root, subPathsOnly);
        
        /* TODO: The URL uniqueness algorithm is working, but this call is adding all of the matching
        links on a page and they're being written once for each page this is parsing. So, we can't just
        pass in the links on each page here. These need to be deduplicate, too. */
        await writer.WriteAllAsync(distinctUrls);
        
        return distinctUrls.ToArray();
    }

    public static async Task<IBrowser> Setup()
    {
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();
        IBrowser? browser = await Puppeteer.LaunchAsync(
            new LaunchOptions { Headless = true }
        );
        
        return browser;
    }

    private static bool IsSubPath(Uri parentUri, Uri childUri)
    {
        if (!parentUri.IsAbsoluteUri || !childUri.IsAbsoluteUri)
        {
            return false;
        }
        
        return parentUri.IsBaseOf(childUri);
    }

    private static string LeftPart(string url)
    {
        Uri uri = new(url);
        return uri.GetLeftPart(UriPartial.Path); 
    }

    private static HashSet<string> DistinctUris(string[] urls, Uri root, bool subPathsOnly)
    {
        var distinctUrls = new HashSet<string>();

        // Remove duplicate URLs by normalizing URLs without fragments and query strings
        foreach (string u in urls)
        {
            if (!Uri.TryCreate(u, UriKind.Absolute, out Uri? url)) continue;
            
            string normalized = url.GetLeftPart(UriPartial.Path);

            // Don't add paths that aren't subpaths when subPathsOnly is true
            if (!subPathsOnly || IsSubPath(root, new Uri(normalized)))
            {
                distinctUrls.Add(normalized);
            }
        }
        return distinctUrls;
    }
    #endregion
}
