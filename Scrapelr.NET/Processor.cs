using System.Threading.Channels;
using PuppeteerSharp;
using Scrapelr.NET.Link;

namespace Scrapelr.NET;

/// <summary>
/// Reads collected links from a channel, renders each page to a PDF and merges the results into a
/// single PDF ordered by <see cref="Node.Order"/>.
/// </summary>
public class Processor : IProcessor
{
    private readonly IBrowser _browser;
    private readonly ChannelReader<Node> _reader;
    private readonly int _maxConcurrency;

    /// <summary>
    /// Creates a processor for a single crawl.
    /// </summary>
    /// <param name="browser">The browser used to render pages. Its lifetime is owned by the caller.</param>
    /// <param name="reader">The channel the collector publishes links to.</param>
    /// <param name="maxConcurrency">The maximum number of pages rendered at once.</param>
    public Processor(IBrowser browser, ChannelReader<Node> reader, int maxConcurrency = 4)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);

        _browser = browser;
        _reader = reader;
        _maxConcurrency = maxConcurrency;
    }

    /// <summary>
    /// Renders every link read from the channel until it completes, then merges the pages into
    /// <paramref name="outputFile"/>. Pages that fail to render are reported and skipped.
    /// </summary>
    /// <param name="outputFile">Where to save the merged PDF.</param>
    /// <exception cref="InvalidOperationException">Thrown when no page could be rendered.</exception>
    public async Task Process(string outputFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFile);

        DirectoryInfo tempDir = Directory.CreateTempSubdirectory("scrapelr-");

        try
        {
            using var throttle = new SemaphoreSlim(_maxConcurrency);
            List<Task<string?>> renders = [];

            await foreach (Node node in _reader.ReadAllAsync())
            {
                if (string.IsNullOrWhiteSpace(node.Url)) continue;

                await throttle.WaitAsync();
                renders.Add(Render(node, tempDir.FullName, throttle));
            }

            // Pages are named by Node.Order, which the collector assigns uniquely per crawl, so
            // sorting by file name restores the crawl order however the renders finished.
            string[] pdfs = (await Task.WhenAll(renders))
                .OfType<string>()
                .OrderBy(path => int.Parse(Path.GetFileNameWithoutExtension(path)))
                .ToArray();

            if (pdfs.Length == 0)
            {
                throw new InvalidOperationException("No pages were rendered, so no PDF was written.");
            }

            PdfMerger.Merge(pdfs, outputFile);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Renders one page to a PDF in <paramref name="directory"/>, releasing a
    /// <paramref name="throttle"/> slot when done.
    /// </summary>
    /// <returns>The path of the rendered PDF, or null if the page failed to render.</returns>
    private async Task<string?> Render(Node node, string directory, SemaphoreSlim throttle)
    {
        try
        {
            Console.WriteLine(node.Url);
            string path = Path.Combine(directory, $"{node.Order}.pdf");

            await using IPage page = await _browser.NewPageAsync();
            await page.GoToAsync(node.Url);
            await page.PdfAsync(path);

            return path;
        }
        catch (Exception ex) when (ex is PuppeteerException or TimeoutException)
        {
            await Console.Error.WriteLineAsync($"Skipping {node.Url}: {ex.Message}");
            return null;
        }
        finally
        {
            throttle.Release();
        }
    }
}
