using Scrapelr.NET.Cli;
using Scrapelr.NET.Tests.Helpers;

namespace Scrapelr.NET.Tests.Cli;

public class CrawlSettingsTests
{
    private const string Root = "https://example.com/docs/";

    [Test]
    public async Task Validate_RootAndOutputFile_Succeeds()
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate()).IsNull();
    }

    [Test]
    public async Task Defaults_MatchCrawlOptionsDefaults()
    {
        var settings = new CrawlSettings { Root = Root };

        await Assert.That(settings.ToCrawlOptions()).IsEqualTo(new CrawlOptions());
    }

    [Test]
    public async Task EffectiveMaxDepth_DefaultsToOneWithRoot()
    {
        var settings = new CrawlSettings { Root = Root };

        await Assert.That(settings.MaxDepth).IsNull();
        await Assert.That(settings.EffectiveMaxDepth).IsEqualTo(1);
    }

    [Test]
    public async Task EffectiveMaxDepth_DefaultsToZeroWithInputFile()
    {
        var settings = new CrawlSettings { InputFile = "urls.txt" };

        await Assert.That(settings.EffectiveMaxDepth).IsEqualTo(0);
        await Assert.That(settings.ToCrawlOptions().MaxDepth).IsEqualTo(0);
    }

    [Test]
    [Arguments(0)]
    [Arguments(3)]
    public async Task EffectiveMaxDepth_UsesExplicitMaxDepth(int maxDepth)
    {
        await Assert.That(new CrawlSettings { Root = Root, MaxDepth = maxDepth }.EffectiveMaxDepth).IsEqualTo(maxDepth);
        await Assert.That(new CrawlSettings { InputFile = "urls.txt", MaxDepth = maxDepth }.EffectiveMaxDepth).IsEqualTo(maxDepth);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("  ")]
    public async Task Validate_NeitherRootNorInputFile_Fails(string? value)
    {
        var settings = new CrawlSettings { Root = value, InputFile = value, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate()).IsEqualTo("Specify --root or --inputFile.");
    }

    [Test]
    public async Task Validate_RootAndInputFile_Fails()
    {
        var settings = new CrawlSettings { Root = Root, InputFile = "urls.txt", OutputFile = "out.pdf" };

        await Assert.That(settings.Validate()).IsEqualTo("Specify --root or --inputFile, not both.");
    }

    [Test]
    public async Task Validate_InputFile_Succeeds()
    {
        using var temp = new TempDirectory();
        string urls = temp.File("urls.txt");
        await File.WriteAllLinesAsync(urls, [Root]);
        var settings = new CrawlSettings { InputFile = urls, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate()).IsNull();
    }

    [Test]
    public async Task Validate_MissingInputFile_Fails()
    {
        using var temp = new TempDirectory();
        string urls = temp.File("missing.txt");
        var settings = new CrawlSettings { InputFile = urls, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate()).IsEqualTo($"--inputFile not found: {urls}");
    }

    [Test]
    public async Task Validate_InputFileWithoutUrls_Fails()
    {
        using var temp = new TempDirectory();
        string urls = temp.File("urls.txt");
        await File.WriteAllLinesAsync(urls, ["# nothing yet", ""]);
        var settings = new CrawlSettings { InputFile = urls, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate()).IsEqualTo($"--inputFile lists no URLs: {urls}");
    }

    [Test]
    public async Task Validate_InputFileWithInvalidLine_ReportsLine()
    {
        using var temp = new TempDirectory();
        string urls = temp.File("urls.txt");
        await File.WriteAllLinesAsync(urls, [Root, "docs/relative"]);
        var settings = new CrawlSettings { InputFile = urls, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate())
            .IsEqualTo($"--inputFile {urls}: Line 2 isn't an absolute http or https URL: docs/relative");
    }

    [Test]
    public async Task Validate_InputFileIsDirectory_Fails()
    {
        using var temp = new TempDirectory();
        var settings = new CrawlSettings { InputFile = temp.FullName, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate()).IsEqualTo($"--inputFile not found: {temp.FullName}");
    }

    [Test]
    public async Task GetStartUrls_WithRoot_ReturnsRoot()
    {
        var settings = new CrawlSettings { Root = Root };

        await Assert.That(settings.GetStartUrls()).IsEquivalentTo([Root]);
    }

    [Test]
    public async Task GetStartUrls_WithInputFile_ReturnsListedUrlsInOrder()
    {
        using var temp = new TempDirectory();
        string urls = temp.File("urls.txt");
        await File.WriteAllLinesAsync(urls, ["https://example.com/b", "# skip", "https://example.com/a"]);
        var settings = new CrawlSettings { InputFile = urls };

        await Assert.That(settings.GetStartUrls())
            .IsEquivalentTo(["https://example.com/b", "https://example.com/a"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("docs/index.html")]
    [Arguments("ftp://example.com/docs/")]
    [Arguments("file:///Users/me/index.html")]
    [Arguments("not a url")]
    public async Task Validate_NonHttpRoot_Fails(string root)
    {
        var settings = new CrawlSettings { Root = root, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate()).StartsWith("--root must be an absolute http or https URL");
    }

    [Test]
    [Arguments("http://example.com/")]
    [Arguments("https://example.com:8443/docs/")]
    public async Task Validate_HttpRoot_Succeeds(string root)
    {
        var settings = new CrawlSettings { Root = root, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate()).IsNull();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("  ")]
    public async Task Validate_MissingOutputFile_Fails(string? outputFile)
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = outputFile };

        await Assert.That(settings.Validate()).IsEqualTo("--outputFile is required.");
    }

    [Test]
    public async Task Validate_OutputFileIsExistingDirectory_Fails()
    {
        using var temp = new TempDirectory();
        var settings = new CrawlSettings { Root = Root, OutputFile = temp.FullName };

        await Assert.That(settings.Validate()).StartsWith("--outputFile must be a file");
    }

    [Test]
    public async Task Validate_OutputFileEndsWithSeparator_Fails()
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = "missing-dir" + Path.DirectorySeparatorChar };

        await Assert.That(settings.Validate()).StartsWith("--outputFile must be a file");
    }

    [Test]
    public async Task Validate_OutputFileInMissingDirectory_Succeeds()
    {
        using var temp = new TempDirectory();
        var settings = new CrawlSettings { Root = Root, OutputFile = temp.File("new", "out.pdf") };

        await Assert.That(settings.Validate()).IsNull();
    }

    [Test]
    public async Task Validate_NegativeMaxDepth_Fails()
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = "out.pdf", MaxDepth = -1 };

        await Assert.That(settings.Validate()).IsEqualTo("--maxDepth can't be negative.");
    }

    [Test]
    public async Task Validate_PathOnlyWithoutDomainOnly_Fails()
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = "out.pdf", PathOnly = true, DomainOnly = false };

        await Assert.That(settings.Validate()).IsEqualTo("--domainOnly can't be false while --pathOnly is true.");
    }

    [Test]
    [Arguments(false, true)]
    [Arguments(false, false)]
    public async Task Validate_PathOnlyDisabled_AllowsEitherDomainOnly(bool pathOnly, bool domainOnly)
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = "out.pdf", PathOnly = pathOnly, DomainOnly = domainOnly };

        await Assert.That(settings.Validate()).IsNull();
    }

    [Test]
    public async Task ToCrawlOptions_CopiesCrawlSettings()
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = "out.pdf", MaxDepth = 4, PathOnly = false, DomainOnly = false };

        CrawlOptions options = settings.ToCrawlOptions();

        await Assert.That(options).IsEqualTo(new CrawlOptions { MaxDepth = 4, PathOnly = false, DomainOnly = false });
    }
}
