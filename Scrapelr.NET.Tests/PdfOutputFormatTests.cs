using NSubstitute;
using PuppeteerSharp;
using Scrapelr.NET.Link;
using Scrapelr.NET.Tests.Helpers;
using TUnit.Assertions.Enums;

namespace Scrapelr.NET.Tests;

public class PdfOutputFormatTests
{
    private readonly PdfOutputFormat _format = new();

    [Test]
    public async Task FileExtension_IsPdf()
    {
        await Assert.That(_format.FileExtension).IsEqualTo(".pdf");
    }

    [Test]
    public async Task Render_PrintsThePageToThePath()
    {
        IPage page = Substitute.For<IPage>();

        await _format.Render(page, new Node("https://example.com/", 0, 0), "/tmp/page.pdf");

        await page.Received(1).PdfAsync("/tmp/page.pdf");
    }

    [Test]
    public async Task Merge_CombinesPagesInOrder()
    {
        using var temp = new TempDirectory();
        string first = temp.File("0.pdf");
        string second = temp.File("1.pdf");
        string output = temp.File("out.pdf");
        TestPdf.Create(first, 100);
        TestPdf.Create(second, 110);

        _format.Merge([second, first], output);

        await Assert.That(TestPdf.PageWidths(output)).IsEquivalentTo([110d, 100d], CollectionOrdering.Matching);
    }
}
