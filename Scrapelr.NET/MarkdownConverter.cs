using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using ReverseMarkdown;
using SmartReader;

namespace Scrapelr.NET;

/// <summary>
/// Converts a page's HTML into a Markdown section headed by the page title and its source URL.
/// </summary>
public static partial class MarkdownConverter
{
    /// <summary>
    /// Converts <paramref name="html"/> into a Markdown section. Unless <paramref name="fullPage"/> is
    /// set, only the page's main content is kept, as a browser's reader view would show it; pages that
    /// don't look like articles fall back to the full page. Links and images are made absolute.
    /// </summary>
    /// <remarks>
    /// The section's heading is the page's only <c>&lt;h1&gt;</c> if it has exactly one, since titles often
    /// carry a site name suffix; otherwise the extracted article title, then <paramref name="title"/>, then
    /// <paramref name="url"/>.
    /// </remarks>
    /// <param name="html">The page's HTML.</param>
    /// <param name="url">The page's URL, used as the base for relative links.</param>
    /// <param name="title">The page's title, if known.</param>
    /// <param name="fullPage">Convert the whole page rather than just its main content.</param>
    /// <returns>The section, starting with a level 1 heading.</returns>
    public static string Convert(string html, string url, string? title, bool fullPage = false)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        IHtmlDocument document = new HtmlParser().ParseDocument(html);
        IElement[] h1s = document.QuerySelectorAll("h1").ToArray();

        string? content = null;
        string? heading = h1s.Length == 1 ? h1s[0].TextContent : null;

        if (!fullPage && ExtractArticle(html, url) is { } article)
        {
            content = article.Content;
            heading = FirstNonBlank(heading, article.Title);
        }

        content ??= PrepareFullPage(document, url);

        heading = FirstNonBlank(heading, title, url)!;
        heading = Whitespace().Replace(heading, " ").Trim();
        string body = RemoveLeadingHeading(CreateConverter().Convert(content).Trim(), heading);

        return body.Length == 0
            ? $"# {heading}\n\nSource: <{url}>\n"
            : $"# {heading}\n\nSource: <{url}>\n\n{body}\n";
    }

    private static Article? ExtractArticle(string html, string url)
    {
        try
        {
            Article article = new Reader(url, html).GetArticle();
            return article.IsReadable && !string.IsNullOrWhiteSpace(article.Content) ? article : null;
        }
        catch (Exception)
        {
            // Extraction is a best effort. Anything it can't handle is converted in full instead.
            return null;
        }
    }

    /// <summary>
    /// Strips non-content elements from the page and makes its links and images absolute.
    /// </summary>
    private static string PrepareFullPage(IHtmlDocument document, string url)
    {
        var baseUri = new Uri(url);

        foreach (var element in document.QuerySelectorAll("script, style, noscript, template"))
        {
            element.Remove();
        }

        foreach (var (selector, attribute) in new[] { ("a[href]", "href"), ("img[src]", "src") })
        {
            foreach (var element in document.QuerySelectorAll(selector))
            {
                if (Uri.TryCreate(baseUri, element.GetAttribute(attribute), out Uri? absolute))
                {
                    element.SetAttribute(attribute, absolute.AbsoluteUri);
                }
            }
        }

        return document.Body?.InnerHtml ?? "";
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    /// <summary>
    /// Removes a level 1 heading at the start of <paramref name="body"/> that repeats <paramref name="heading"/>.
    /// </summary>
    private static string RemoveLeadingHeading(string body, string heading)
    {
        Match match = LeadingHeading().Match(body);
        return match.Success && string.Equals(match.Groups[1].Value.Trim(), heading, StringComparison.OrdinalIgnoreCase)
            ? body[match.Length..].TrimStart()
            : body;
    }

    // Converters are created per call because ReverseMarkdown doesn't document Converter as thread-safe,
    // and pages are converted concurrently. The default flavor is deliberate: it already writes fenced code
    // blocks and pipe tables, while the GitHub and CommonMark flavors leave inline elements such as
    // <strong> and <a> as raw HTML.
    private static Converter CreateConverter() => new(new Config
    {
        Formatting = { RemoveComments = true },
        Tags = { Unknown = Config.UnknownTagsOption.Bypass }
    });

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\A#[ \t]+([^\r\n]*)(\r?\n|\z)")]
    private static partial Regex LeadingHeading();
}
