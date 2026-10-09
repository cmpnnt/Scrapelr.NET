namespace Scrapelr.NET.Cli;

/// <summary>
/// Reads the starting URLs for <c>--inputFile</c>: one absolute http or https URL per line. Blank lines
/// and lines starting with <c>#</c> are ignored.
/// </summary>
public static class UrlFile
{
    /// <summary>
    /// Reads the URLs listed in the file at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The URLs in file order.</returns>
    /// <exception cref="FormatException">Thrown when a line isn't an absolute http or https URL.</exception>
    public static IReadOnlyList<string> Read(string path) => Parse(File.ReadLines(path));

    /// <summary>
    /// Parses URLs from the lines of a URL file.
    /// </summary>
    /// <param name="lines">The file's lines.</param>
    /// <returns>The URLs in line order.</returns>
    /// <exception cref="FormatException">Thrown when a line isn't an absolute http or https URL.</exception>
    public static IReadOnlyList<string> Parse(IEnumerable<string> lines)
    {
        List<string> urls = [];
        int lineNumber = 0;

        foreach (string raw in lines)
        {
            lineNumber++;
            string line = raw.Trim();

            if (line.Length == 0 || line.StartsWith('#')) continue;

            if (!IsHttpUrl(line))
            {
                throw new FormatException($"Line {lineNumber} isn't an absolute http or https URL: {line}");
            }

            urls.Add(line);
        }

        return urls;
    }

    internal static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
