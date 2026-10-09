using PuppeteerSharp;
using Scrapelr.NET.Link;

namespace Scrapelr.NET;

/// <summary>
/// Turns loaded pages into one output document. A <see cref="Processor"/> renders each page to its own
/// temporary file, then merges those files, in crawl order, into the output.
/// </summary>
public interface IOutputFormat
{
    /// <summary>
    /// The extension, including the leading dot, of the per-page files <see cref="Render"/> writes.
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Writes one loaded page to <paramref name="path"/>.
    /// </summary>
    /// <param name="page">The page, already navigated to <paramref name="node"/>.</param>
    /// <param name="node">The link the page was loaded from.</param>
    /// <param name="path">The file to write.</param>
    Task Render(IPage page, Node node, string path);

    /// <summary>
    /// Combines the per-page files, in the order given, into <paramref name="outputFile"/>. The output's
    /// directory is created if it doesn't exist and an existing file is overwritten.
    /// </summary>
    /// <param name="pages">Files written by <see cref="Render"/>, in output order.</param>
    /// <param name="outputFile">Where to save the combined document.</param>
    void Merge(IReadOnlyList<string> pages, string outputFile);
}
