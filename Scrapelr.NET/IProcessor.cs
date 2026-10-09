namespace Scrapelr.NET;

/// <summary>
/// Consumes the links published by an <see cref="ICollector"/> and turns them into a single document.
/// </summary>
public interface IProcessor
{
    /// <summary>
    /// Processes every collected link and saves the result to <paramref name="outputFile"/>.
    /// </summary>
    /// <param name="outputFile">Where to save the merged document.</param>
    Task Process(string outputFile);
}
