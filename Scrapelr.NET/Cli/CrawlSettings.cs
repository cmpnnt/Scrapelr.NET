using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Scrapelr.NET.Cli;

/// <summary>
/// Command line options for <see cref="CrawlCommand"/>.
/// </summary>
public sealed class CrawlSettings : CommandSettings
{
    [CommandOption("-r|--root <URL>")]
    [Description("The URL to start crawling from.")]
    public string? Root { get; init; }

    [CommandOption("-o|--outputFile <FILE>")]
    [Description("Where to save the merged PDF. Existing files are overwritten.")]
    public string? OutputFile { get; init; }

    [CommandOption("-m|--maxDepth <DEPTH>")]
    [Description("How many links deep to follow from the root page. The root page is depth 0.")]
    [DefaultValue(1)]
    public int MaxDepth { get; init; } = 1;

    [CommandOption("-p|--pathOnly <BOOL>")]
    [Description("Only follow links beneath the root URL's path. Implies --domainOnly.")]
    [DefaultValue(true)]
    public bool PathOnly { get; init; } = true;

    [CommandOption("-d|--domainOnly <BOOL>")]
    [Description("Only follow links on the root URL's scheme, host and port.")]
    [DefaultValue(true)]
    public bool DomainOnly { get; init; } = true;

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Root))
        {
            return ValidationResult.Error("--root is required.");
        }

        if (!Uri.TryCreate(Root, UriKind.Absolute, out Uri? root) ||
            (root.Scheme != Uri.UriSchemeHttp && root.Scheme != Uri.UriSchemeHttps))
        {
            return ValidationResult.Error($"--root must be an absolute http or https URL: {Root}");
        }

        if (string.IsNullOrWhiteSpace(OutputFile))
        {
            return ValidationResult.Error("--outputFile is required.");
        }

        if (Directory.Exists(OutputFile) || Path.EndsInDirectorySeparator(OutputFile))
        {
            return ValidationResult.Error($"--outputFile must be a file, not a directory: {OutputFile}");
        }

        if (MaxDepth < 0)
        {
            return ValidationResult.Error("--maxDepth can't be negative.");
        }

        if (PathOnly && !DomainOnly)
        {
            return ValidationResult.Error("--domainOnly can't be false while --pathOnly is true.");
        }

        return ValidationResult.Success();
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
