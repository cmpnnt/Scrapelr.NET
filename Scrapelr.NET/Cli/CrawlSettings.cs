namespace Scrapelr.NET.Cli;

/// <summary>
/// Command line options for a crawl, as bound by <see cref="CrawlCommand"/>.
/// </summary>
public sealed class CrawlSettings
{
    /// <summary>The URL to start crawling from.</summary>
    public string? Root { get; init; }

    /// <summary>Where to save the merged PDF.</summary>
    public string? OutputFile { get; init; }

    /// <summary>How many links deep to follow from the root page. The root page is depth 0.</summary>
    public int MaxDepth { get; init; } = 1;

    /// <summary>Only follow links beneath the root URL's path. Implies <see cref="DomainOnly"/>.</summary>
    public bool PathOnly { get; init; } = true;

    /// <summary>Only follow links on the root URL's scheme, host and port.</summary>
    public bool DomainOnly { get; init; } = true;

    /// <summary>
    /// Checks the settings describe a crawl that can run.
    /// </summary>
    /// <returns>An error message, or null when the settings are valid.</returns>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Root))
        {
            return "--root is required.";
        }

        if (!Uri.TryCreate(Root, UriKind.Absolute, out Uri? root) ||
            (root.Scheme != Uri.UriSchemeHttp && root.Scheme != Uri.UriSchemeHttps))
        {
            return $"--root must be an absolute http or https URL: {Root}";
        }

        if (string.IsNullOrWhiteSpace(OutputFile))
        {
            return "--outputFile is required.";
        }

        if (Directory.Exists(OutputFile) || Path.EndsInDirectorySeparator(OutputFile))
        {
            return $"--outputFile must be a file, not a directory: {OutputFile}";
        }

        if (MaxDepth < 0)
        {
            return "--maxDepth can't be negative.";
        }

        if (PathOnly && !DomainOnly)
        {
            return "--domainOnly can't be false while --pathOnly is true.";
        }

        return null;
    }

    /// <summary>
    /// Converts these settings to the options consumed by <see cref="Collector"/>.
    /// </summary>
    public CrawlOptions ToCrawlOptions() => new()
    {
        MaxDepth = MaxDepth,
        PathOnly = PathOnly,
        DomainOnly = DomainOnly
    };
}
