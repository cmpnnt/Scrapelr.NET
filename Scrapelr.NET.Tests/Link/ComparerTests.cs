using Scrapelr.NET.Link;

namespace Scrapelr.NET.Tests.Link;

public class ComparerTests
{
    private readonly Comparer _comparer = new();

    [Test]
    public async Task Equals_IgnoresUrlCase()
    {
        var a = new Node("https://Example.com/Docs", 0, 0);
        var b = new Node("https://example.com/docs", 0, 0);

        await Assert.That(_comparer.Equals(a, b)).IsTrue();
        await Assert.That(_comparer.GetHashCode(a)).IsEqualTo(_comparer.GetHashCode(b));
    }

    [Test]
    public async Task Equals_IgnoresDepthAndOrder()
    {
        var a = new Node("https://example.com/docs", 1, 5);
        var b = new Node("https://example.com/docs", 3, 9);

        await Assert.That(_comparer.Equals(a, b)).IsTrue();
        await Assert.That(_comparer.GetHashCode(a)).IsEqualTo(_comparer.GetHashCode(b));
    }

    [Test]
    public async Task Equals_DistinguishesDifferentUrls()
    {
        var a = new Node("https://example.com/docs", 0, 0);
        var b = new Node("https://example.com/blog", 0, 0);

        await Assert.That(_comparer.Equals(a, b)).IsFalse();
    }

    [Test]
    public async Task HashSet_UsingComparer_DeduplicatesByUrl()
    {
        var set = new HashSet<Node>(_comparer)
        {
            new("https://example.com/a", 0, 0),
            new("https://EXAMPLE.com/A", 1, 1),
            new("https://example.com/b", 1, 2)
        };

        await Assert.That(set.Count).IsEqualTo(2);
    }
}
