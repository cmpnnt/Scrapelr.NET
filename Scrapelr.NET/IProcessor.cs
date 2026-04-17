namespace Scrapelr.NET;

public interface IProcessor
{
    /// <summary>
    /// 
    /// </summary>
    Task Process(string outputFile);
}