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

        await Assert.That(settings.Validate().Successful).IsTrue();
    }

    [Test]
    public async Task Defaults_MatchCrawlOptionsDefaults()
    {
        var settings = new CrawlSettings();

        await Assert.That(settings.MaxDepth).IsEqualTo(1);
        await Assert.That(settings.PathOnly).IsTrue();
        await Assert.That(settings.DomainOnly).IsTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("  ")]
    public async Task Validate_MissingRoot_Fails(string? root)
    {
        var settings = new CrawlSettings { Root = root, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate().Message).IsEqualTo("--root is required.");
    }

    [Test]
    [Arguments("docs/index.html")]
    [Arguments("ftp://example.com/docs/")]
    [Arguments("file:///Users/me/index.html")]
    [Arguments("not a url")]
    public async Task Validate_NonHttpRoot_Fails(string root)
    {
        var settings = new CrawlSettings { Root = root, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate().Message).StartsWith("--root must be an absolute http or https URL");
    }

    [Test]
    [Arguments("http://example.com/")]
    [Arguments("https://example.com:8443/docs/")]
    public async Task Validate_HttpRoot_Succeeds(string root)
    {
        var settings = new CrawlSettings { Root = root, OutputFile = "out.pdf" };

        await Assert.That(settings.Validate().Successful).IsTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("  ")]
    public async Task Validate_MissingOutputFile_Fails(string? outputFile)
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = outputFile };

        await Assert.That(settings.Validate().Message).IsEqualTo("--outputFile is required.");
    }

    [Test]
    public async Task Validate_OutputFileIsExistingDirectory_Fails()
    {
        using var temp = new TempDirectory();
        var settings = new CrawlSettings { Root = Root, OutputFile = temp.FullName };

        await Assert.That(settings.Validate().Message).StartsWith("--outputFile must be a file");
    }

    [Test]
    public async Task Validate_OutputFileEndsWithSeparator_Fails()
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = "missing-dir" + Path.DirectorySeparatorChar };

        await Assert.That(settings.Validate().Message).StartsWith("--outputFile must be a file");
    }

    [Test]
    public async Task Validate_OutputFileInMissingDirectory_Succeeds()
    {
        using var temp = new TempDirectory();
        var settings = new CrawlSettings { Root = Root, OutputFile = temp.File("new", "out.pdf") };

        await Assert.That(settings.Validate().Successful).IsTrue();
    }

    [Test]
    public async Task Validate_NegativeMaxDepth_Fails()
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = "out.pdf", MaxDepth = -1 };

        await Assert.That(settings.Validate().Message).IsEqualTo("--maxDepth can't be negative.");
    }

    [Test]
    public async Task Validate_PathOnlyWithoutDomainOnly_Fails()
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = "out.pdf", PathOnly = true, DomainOnly = false };

        await Assert.That(settings.Validate().Message).IsEqualTo("--domainOnly can't be false while --pathOnly is true.");
    }

    [Test]
    [Arguments(false, true)]
    [Arguments(false, false)]
    public async Task Validate_PathOnlyDisabled_AllowsEitherDomainOnly(bool pathOnly, bool domainOnly)
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = "out.pdf", PathOnly = pathOnly, DomainOnly = domainOnly };

        await Assert.That(settings.Validate().Successful).IsTrue();
    }

    [Test]
    public async Task ToCrawlOptions_CopiesCrawlSettings()
    {
        var settings = new CrawlSettings { Root = Root, OutputFile = "out.pdf", MaxDepth = 4, PathOnly = false, DomainOnly = false };

        CrawlOptions options = settings.ToCrawlOptions();

        await Assert.That(options).IsEqualTo(new CrawlOptions { MaxDepth = 4, PathOnly = false, DomainOnly = false });
    }
}
