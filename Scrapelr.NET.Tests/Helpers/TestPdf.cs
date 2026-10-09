using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Scrapelr.NET.Tests.Helpers;

/// <summary>
/// Builds and inspects small PDFs. Pages are told apart by width, which survives merging.
/// </summary>
internal static class TestPdf
{
    public static void Create(string path, params double[] pageWidths)
    {
        using var document = new PdfDocument();
        foreach (double width in pageWidths)
        {
            PdfPage page = document.AddPage();
            page.Width = XUnit.FromPoint(width);
            page.Height = XUnit.FromPoint(500);
        }
        document.Save(path);
    }

    public static double[] PageWidths(string path)
    {
        using PdfDocument document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return document.Pages.Cast<PdfPage>().Select(page => Math.Round(page.Width.Point)).ToArray();
    }

    public static int PageCount(string path)
    {
        using PdfDocument document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return document.PageCount;
    }
}
