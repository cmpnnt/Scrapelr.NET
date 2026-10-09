namespace Scrapelr.NET.Tests;

public class MarkdownConverterTests
{
    private const string Url = "https://example.com/docs/containers";

    /// <summary>A documentation page with enough prose for reader-view extraction to treat it as an article.</summary>
    internal const string ArticleHtml = """
        <html><head><title>Containers overview | Docs</title></head><body>
        <nav><a href="/">Home</a> | <a href="/docs/">Docs</a></nav>
        <main><article>
        <h1>Containers overview</h1>
        <p>Containers package an <strong>app</strong> with its dependencies. See <a href="guide">the guide</a> for more
        detail on how images are layered, cached and shared between hosts, and why that makes deployments repeatable.</p>
        <p>A second paragraph describes registries, tags and how a runtime pulls an image before starting a container,
        so that reader view has plenty of text to work with when it scores this element as the main content.</p>
        <h2>Building an image</h2>
        <pre><code>docker build -t app .</code></pre>
        <table><tr><th>Option</th><th>Meaning</th></tr><tr><td>-t</td><td>Tag</td></tr></table>
        <img src="/img/layers.png" alt="Layers">
        </article></main>
        <footer>Copyright Example Corp</footer>
        <script>console.log("tracking")</script>
        </body></html>
        """;

    [Test]
    public async Task Convert_StartsWithTitleHeadingAndSource()
    {
        string markdown = MarkdownConverter.Convert(ArticleHtml, Url, "Containers overview | Docs");

        await Assert.That(markdown).StartsWith($"# Containers overview\n\nSource: <{Url}>\n\n");
    }

    [Test]
    public async Task Convert_KeepsMainContentFormatting()
    {
        string markdown = MarkdownConverter.Convert(ArticleHtml, Url, null);

        await Assert.That(markdown).Contains("Containers package an **app** with its dependencies.");
        await Assert.That(markdown).Contains("## Building an image");
        await Assert.That(markdown).Contains("```\ndocker build -t app .\n```");
        await Assert.That(markdown).Contains("| Option | Meaning |");
        await Assert.That(markdown).Contains("| -t | Tag |");
    }

    [Test]
    public async Task Convert_MakesLinksAndImagesAbsolute()
    {
        string markdown = MarkdownConverter.Convert(ArticleHtml, Url, null);

        await Assert.That(markdown).Contains("[the guide](https://example.com/docs/guide)");
        await Assert.That(markdown).Contains("![Layers](https://example.com/img/layers.png)");
    }

    [Test]
    public async Task Convert_DropsPageChromeByDefault()
    {
        string markdown = MarkdownConverter.Convert(ArticleHtml, Url, null);

        await Assert.That(markdown).DoesNotContain("[Home]");
        await Assert.That(markdown).DoesNotContain("Copyright Example Corp");
        await Assert.That(markdown).DoesNotContain("tracking");
    }

    [Test]
    public async Task Convert_DoesNotRepeatTheTitleAsAHeading()
    {
        string markdown = MarkdownConverter.Convert(ArticleHtml, Url, null);

        await Assert.That(markdown.Split('\n').Count(line => line.StartsWith("# "))).IsEqualTo(1);
    }

    [Test]
    public async Task Convert_FullPage_KeepsPageChromeButNotScripts()
    {
        string markdown = MarkdownConverter.Convert(ArticleHtml, Url, "Containers overview", fullPage: true);

        await Assert.That(markdown).Contains("[Home](https://example.com/)");
        await Assert.That(markdown).Contains("Copyright Example Corp");
        await Assert.That(markdown).Contains("[the guide](https://example.com/docs/guide)");
        await Assert.That(markdown).DoesNotContain("tracking");
    }

    [Test]
    public async Task Convert_FullPage_UsesOnlyH1AsHeadingWithoutRepeatingIt()
    {
        const string html = "<html><body><h1>Release notes</h1><p>Fixed things.</p></body></html>";

        string markdown = MarkdownConverter.Convert(html, Url, "Release notes | Example Docs", fullPage: true);

        await Assert.That(markdown).IsEqualTo($"# Release notes\n\nSource: <{Url}>\n\nFixed things.\n");
    }

    [Test]
    public async Task Convert_SeveralH1s_UsesTitleAndKeepsThem()
    {
        const string html = "<html><body><h1>Part one</h1><p>First.</p><h1>Part two</h1><p>Second.</p></body></html>";

        string markdown = MarkdownConverter.Convert(html, Url, "Guide", fullPage: true);

        await Assert.That(markdown).StartsWith("# Guide\n");
        await Assert.That(markdown).Contains("# Part one");
        await Assert.That(markdown).Contains("# Part two");
    }

    [Test]
    public async Task Convert_NoH1_UsesArticleTitle()
    {
        string html = MarkdownConverterTests.ArticleHtml.Replace("<h1>Containers overview</h1>", "");

        string markdown = MarkdownConverter.Convert(html, Url, "Fallback title");

        await Assert.That(markdown).StartsWith("# Containers overview | Docs\n");
    }

    [Test]
    public async Task Convert_PageThatIsNotAnArticle_FallsBackToFullPage()
    {
        const string html = "<html><body><ul><li><a href=\"/a\">A</a></li><li><a href=\"/b\">B</a></li></ul></body></html>";

        string markdown = MarkdownConverter.Convert(html, Url, "Index");

        await Assert.That(markdown).Contains("[A](https://example.com/a)");
        await Assert.That(markdown).Contains("[B](https://example.com/b)");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Convert_WithoutTitle_UsesUrlAsHeading(string? title)
    {
        string markdown = MarkdownConverter.Convert("<html><body><p>Hi</p></body></html>", Url, title, fullPage: true);

        await Assert.That(markdown).StartsWith($"# {Url}\n");
    }

    [Test]
    public async Task Convert_CollapsesWhitespaceInTitle()
    {
        string markdown = MarkdownConverter.Convert("<html><body><p>Hi</p></body></html>", Url, "  Two\n   lines ", fullPage: true);

        await Assert.That(markdown).StartsWith("# Two lines\n");
    }

    [Test]
    public async Task Convert_EmptyPage_HasHeadingAndSourceOnly()
    {
        string markdown = MarkdownConverter.Convert("<html><body></body></html>", Url, "Empty", fullPage: true);

        await Assert.That(markdown).IsEqualTo($"# Empty\n\nSource: <{Url}>\n");
    }

    [Test]
    public async Task Convert_LeavesNonHttpLinksAlone()
    {
        const string html = "<html><body><p><a href=\"mailto:docs@example.com\">Email us</a></p></body></html>";

        string markdown = MarkdownConverter.Convert(html, Url, "Contact", fullPage: true);

        await Assert.That(markdown).Contains("[Email us](mailto:docs@example.com)");
    }

    [Test]
    public async Task Convert_InvalidArguments_Throw()
    {
        await Assert.That(() => MarkdownConverter.Convert(null!, Url, null)).Throws<ArgumentNullException>();
        await Assert.That(() => MarkdownConverter.Convert("<p></p>", " ", null)).Throws<ArgumentException>();
    }
}
