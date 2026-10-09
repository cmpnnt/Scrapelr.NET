using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Scrapelr.NET;

/// <summary>
/// Combines several PDF files into a single document.
/// </summary>
public static class PdfMerger
{
    /// <summary>
    /// Appends every page of each input file, in the order given, to a new document saved at
    /// <paramref name="outputFile"/>. The output's directory is created if it doesn't exist and an
    /// existing file is overwritten.
    /// </summary>
    /// <param name="inputFiles">The PDF files to combine, in output order.</param>
    /// <param name="outputFile">Where to save the combined PDF.</param>
    /// <exception cref="InvalidOperationException">Thrown when the inputs contain no pages.</exception>
    public static void Merge(IEnumerable<string> inputFiles, string outputFile)
    {
        ArgumentNullException.ThrowIfNull(inputFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFile);

        using var output = new PdfDocument();

        foreach (string file in inputFiles)
        {
            using PdfDocument input = PdfReader.Open(file, PdfDocumentOpenMode.Import);
            foreach (PdfPage page in input.Pages)
            {
                output.AddPage(page);
            }
        }

        if (output.PageCount == 0)
        {
            throw new InvalidOperationException("There are no pages to merge.");
        }

        string fullPath = Path.GetFullPath(outputFile);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        output.Save(fullPath);
    }
}
