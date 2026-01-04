namespace Scrapelr.NET.Link;

public class Comparer : IEqualityComparer<Node>
{
    public bool Equals(Node x, Node y)
    {
        return string.Equals(x.Url, y.Url, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(Node obj)
    {
        // If Url can be null, use: obj.Url?.GetHashCode() ?? 0;
        return obj.Url.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
}