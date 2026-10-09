using Scrapelr.NET.Link;

namespace Scrapelr.NET.Tests.Link;

public class NodeTests
{
    [Test]
    public async Task Constructor_SetsProperties()
    {
        var node = new Node("https://example.com/", 2, 7);

        await Assert.That(node.Url).IsEqualTo("https://example.com/");
        await Assert.That(node.Depth).IsEqualTo(2);
        await Assert.That(node.Order).IsEqualTo(7);
    }

    [Test]
    public async Task Equality_ComparesAllMembers()
    {
        await Assert.That(new Node("https://example.com/", 1, 1)).IsEqualTo(new Node("https://example.com/", 1, 1));
        await Assert.That(new Node("https://example.com/", 1, 1)).IsNotEqualTo(new Node("https://example.com/", 1, 2));
    }
}
