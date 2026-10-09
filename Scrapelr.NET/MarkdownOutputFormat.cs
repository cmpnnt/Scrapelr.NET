using System.Text;
using PuppeteerSharp;
using Scrapelr.NET.Link;

namespace Scrapelr.NET;

/// <summary>
/// Converts each page to a Markdown section with <see cref="MarkdownConverter"/> and joins the sections
/// into a single Markdown file.
/// </summary>
/// <param name="fullPage">Convert whole pages rather than just their main content.</param>
public sealed class MarkdownOutputFormat(bool fullPage = false) : IOutputFormat
{
    private const string Separator = "\n---\n\n";
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public string FileExtension => ".md";

    public async Task Render(IPage page, Node node, string path)
    {
        // The page's own URL reflects any redirects, so relative links resolve against the right base.
        string url = string.IsNullOrWhiteSpace(page.Url) ? node.Url : page.Url;
        string html = await page.GetContentAsync();
        string title = await page.GetTitleAsync();

        await File.WriteAllTextAsync(path, MarkdownConverter.Convert(html, url, title, fullPage), Utf8);
    }

    public void Merge(IReadOnlyList<string> pages, string outputFile)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFile);

        if (pages.Count == 0)
        {
            throw new InvalidOperationException("There are no pages to merge.");
        }

        string document = string.Join(Separator, pages.Select(page => File.ReadAllText(page).TrimEnd() + "\n"));

        string fullPath = Path.GetFullPath(outputFile);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, document, Utf8);
    }
}
