namespace Scrapelr.NET.Cli;

/// <summary>
/// Command line options for a crawl, as bound by <see cref="CrawlCommand"/>.
/// </summary>
public sealed class CrawlSettings
{
    /// <summary>The URL to start crawling from. Mutually exclusive with <see cref="InputFile"/>.</summary>
    public string? Root { get; init; }

    /// <summary>A file listing URLs to start crawling from. Mutually exclusive with <see cref="Root"/>.</summary>
    public string? InputFile { get; init; }

    /// <summary>Where to save the merged PDF.</summary>
    public string? OutputFile { get; init; }

    /// <summary>
    /// How many links deep to follow from each starting page, which is depth 0. When null,
    /// <see cref="EffectiveMaxDepth"/> supplies the default.
    /// </summary>
    public int? MaxDepth { get; init; }

    /// <summary>Only follow links beneath the starting URL's path. Implies <see cref="DomainOnly"/>.</summary>
    public bool PathOnly { get; init; } = true;

    /// <summary>Only follow links on the starting URL's scheme, host and port.</summary>
    public bool DomainOnly { get; init; } = true;

    /// <summary>
    /// <see cref="MaxDepth"/> if set. Otherwise 1 when crawling from <see cref="Root"/>, and 0 when
    /// reading <see cref="InputFile"/> so that, by default, the PDF holds just the listed pages.
    /// </summary>
    public int EffectiveMaxDepth => MaxDepth ?? (HasInputFile ? 0 : 1);

    private bool HasRoot => !string.IsNullOrWhiteSpace(Root);

    private bool HasInputFile => !string.IsNullOrWhiteSpace(InputFile);

    /// <summary>
    /// Checks the settings describe a crawl that can run.
    /// </summary>
    /// <returns>An error message, or null when the settings are valid.</returns>
    public string? Validate()
    {
        if (HasRoot == HasInputFile)
        {
            return HasRoot
                ? "Specify --root or --inputFile, not both."
                : "Specify --root or --inputFile.";
        }

        if (HasRoot && !UrlFile.IsHttpUrl(Root!))
        {
            return $"--root must be an absolute http or https URL: {Root}";
        }

        if (HasInputFile && ValidateInputFile() is { } inputFileError)
        {
            return inputFileError;
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
    /// The URLs to start crawling from: <see cref="Root"/>, or those listed in <see cref="InputFile"/>.
    /// Call after <see cref="Validate"/> succeeds.
    /// </summary>
    public IReadOnlyList<string> GetStartUrls() => HasRoot ? [Root!] : UrlFile.Read(InputFile!);

    /// <summary>
    /// Converts these settings to the options consumed by <see cref="Collector"/>.
    /// </summary>
    public CrawlOptions ToCrawlOptions() => new()
    {
        MaxDepth = EffectiveMaxDepth,
        PathOnly = PathOnly,
        DomainOnly = DomainOnly
    };

    private string? ValidateInputFile()
    {
        if (!File.Exists(InputFile))
        {
            return $"--inputFile not found: {InputFile}";
        }

        try
        {
            return UrlFile.Read(InputFile!).Count == 0 ? $"--inputFile lists no URLs: {InputFile}" : null;
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            return $"--inputFile {InputFile}: {ex.Message}";
        }
    }
}
