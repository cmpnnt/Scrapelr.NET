namespace Scrapelr.NET.Tests;

public class CrawlOptionsTests
{
    [Test]
    public async Task Defaults_FollowOneLevelWithinPath()
    {
        var options = new CrawlOptions();

        await Assert.That(options.MaxDepth).IsEqualTo(1);
        await Assert.That(options.DomainOnly).IsTrue();
        await Assert.That(options.PathOnly).IsTrue();
    }

    [Test]
    [Arguments(true, true, true)]
    [Arguments(true, false, true)]
    [Arguments(false, true, true)]
    [Arguments(false, false, false)]
    public async Task FollowDomain_IsImpliedByEitherOption(bool pathOnly, bool domainOnly, bool expected)
    {
        var options = new CrawlOptions { PathOnly = pathOnly, DomainOnly = domainOnly };

        await Assert.That(options.FollowDomain).IsEqualTo(expected);
    }
}
