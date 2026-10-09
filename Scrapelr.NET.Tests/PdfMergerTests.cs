using Scrapelr.NET.Tests.Helpers;

namespace Scrapelr.NET.Tests;

public class PdfMergerTests
{
    [Test]
    public async Task Merge_AppendsAllPagesInInputOrder()
    {
        using var temp = new TempDirectory();
        string first = temp.File("first.pdf");
        string second = temp.File("second.pdf");
        string output = temp.File("merged.pdf");
        TestPdf.Create(first, 300, 310);
        TestPdf.Create(second, 100);

        PdfMerger.Merge([second, first], output);

        await Assert.That(TestPdf.PageWidths(output)).IsEquivalentTo([100d, 300d, 310d], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Merge_CreatesMissingOutputDirectory()
    {
        using var temp = new TempDirectory();
        string input = temp.File("in.pdf");
        string output = temp.File("a", "b", "merged.pdf");
        TestPdf.Create(input, 200);

        PdfMerger.Merge([input], output);

        await Assert.That(File.Exists(output)).IsTrue();
    }

    [Test]
    public async Task Merge_OverwritesExistingOutput()
    {
        using var temp = new TempDirectory();
        string input = temp.File("in.pdf");
        string output = temp.File("merged.pdf");
        TestPdf.Create(input, 250);
        await File.WriteAllTextAsync(output, "stale");

        PdfMerger.Merge([input], output);

        await Assert.That(TestPdf.PageWidths(output)).IsEquivalentTo([250d]);
    }

    [Test]
    public async Task Merge_RelativeOutputPath_ResolvesAgainstCurrentDirectory()
    {
        using var temp = new TempDirectory();
        string input = temp.File("in.pdf");
        TestPdf.Create(input, 200);
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, temp.File("relative.pdf"));

        PdfMerger.Merge([input], relative);

        await Assert.That(File.Exists(temp.File("relative.pdf"))).IsTrue();
    }

    [Test]
    public async Task Merge_NoInputs_ThrowsAndWritesNothing()
    {
        using var temp = new TempDirectory();
        string output = temp.File("merged.pdf");

        await Assert.That(() => PdfMerger.Merge([], output)).Throws<InvalidOperationException>();
        await Assert.That(File.Exists(output)).IsFalse();
    }

    [Test]
    public async Task Merge_NullInputs_Throws()
    {
        await Assert.That(() => PdfMerger.Merge(null!, "out.pdf")).Throws<ArgumentNullException>();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Merge_BlankOutput_Throws(string output)
    {
        await Assert.That(() => PdfMerger.Merge([], output)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Merge_MissingInput_Throws()
    {
        using var temp = new TempDirectory();

        await Assert.That(() => PdfMerger.Merge([temp.File("missing.pdf")], temp.File("out.pdf")))
            .Throws<FileNotFoundException>();
    }
}
