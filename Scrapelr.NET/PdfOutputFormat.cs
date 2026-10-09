using PuppeteerSharp;
using Scrapelr.NET.Link;

namespace Scrapelr.NET;

/// <summary>
/// Prints each page to a PDF with the browser and merges them into a single PDF.
/// </summary>
public sealed class PdfOutputFormat : IOutputFormat
{
    public string FileExtension => ".pdf";

    public Task Render(IPage page, Node node, string path) => page.PdfAsync(path);

    public void Merge(IReadOnlyList<string> pages, string outputFile) => PdfMerger.Merge(pages, outputFile);
}
