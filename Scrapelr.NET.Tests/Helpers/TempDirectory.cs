namespace Scrapelr.NET.Tests.Helpers;

/// <summary>
/// A uniquely named directory under the system temp path, deleted on dispose.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("scrapelr-tests-");

    public string FullName => _directory.FullName;

    public string File(params string[] parts) => Path.Combine([FullName, ..parts]);

    public void Dispose()
    {
        if (_directory.Exists)
        {
            _directory.Delete(recursive: true);
        }
    }
}
