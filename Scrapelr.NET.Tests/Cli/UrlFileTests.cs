using Scrapelr.NET.Cli;
using Scrapelr.NET.Tests.Helpers;
using TUnit.Assertions.Enums;

namespace Scrapelr.NET.Tests.Cli;

public class UrlFileTests
{
    [Test]
    public async Task Parse_ReturnsUrlsInLineOrder()
    {
        IReadOnlyList<string> urls = UrlFile.Parse(["https://example.com/b", "http://example.com/a"]);

        await Assert.That(urls).IsEquivalentTo(["https://example.com/b", "http://example.com/a"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Parse_IgnoresBlankLinesCommentsAndSurroundingWhitespace()
    {
        IReadOnlyList<string> urls = UrlFile.Parse(
        [
            "# Pages to print",
            "",
            "   ",
            "  https://example.com/a\t",
            "  # indented comment",
            "https://example.com/b"
        ]);

        await Assert.That(urls).IsEquivalentTo(["https://example.com/a", "https://example.com/b"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Parse_KeepsDuplicates()
    {
        // De-duplication is the collector's job, which also normalizes fragments and query strings.
        IReadOnlyList<string> urls = UrlFile.Parse(["https://example.com/a", "https://example.com/a"]);

        await Assert.That(urls.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments("example.com/a")]
    [Arguments("ftp://example.com/a")]
    [Arguments("mailto:someone@example.com")]
    [Arguments("not a url")]
    public async Task Parse_InvalidLine_ThrowsWithLineNumber(string line)
    {
        await Assert.That(() => UrlFile.Parse(["# header", "https://example.com/a", line]))
            .Throws<FormatException>()
            .WithMessage($"Line 3 isn't an absolute http or https URL: {line}");
    }

    [Test]
    public async Task Parse_NoUrls_ReturnsEmpty()
    {
        await Assert.That(UrlFile.Parse(["# nothing here", ""])).IsEmpty();
    }

    [Test]
    public async Task Read_ParsesFile()
    {
        using var temp = new TempDirectory();
        string path = temp.File("urls.txt");
        await File.WriteAllLinesAsync(path, ["# docs", "https://example.com/a", "", "https://example.com/b"]);

        IReadOnlyList<string> urls = UrlFile.Read(path);

        await Assert.That(urls).IsEquivalentTo(["https://example.com/a", "https://example.com/b"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Read_MissingFile_Throws()
    {
        using var temp = new TempDirectory();

        await Assert.That(() => UrlFile.Read(temp.File("missing.txt"))).Throws<FileNotFoundException>();
    }
}
