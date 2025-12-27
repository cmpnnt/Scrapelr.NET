using PuppeteerSharp;

namespace Scrapelr.NET;

public class Collector
{
    public static async Task<string[]?> Collect(IBrowser browser, string uri, bool subPathsOnly = true)
    {
        await using IPage? page = await browser.NewPageAsync();
        
        await page.SetRequestInterceptionAsync(true);
        
        var root = new Uri(LeftPart(uri));
        
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

        if (urls == null) return urls;
        
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
}
