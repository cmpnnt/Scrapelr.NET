using System.Collections.Concurrent;
using NSubstitute;
using PuppeteerSharp;

namespace Scrapelr.NET.Tests.Helpers;

/// <summary>
/// An <see cref="IBrowser"/> substitute backed by an in-memory site map. Each page returns the links
/// registered for its URL and "prints" a one-page PDF whose width identifies the URL.
/// </summary>
internal sealed class FakeBrowser
{
    public const double DefaultWidth = 200;

    private readonly ConcurrentDictionary<string, string[]?> _links = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, double> _widths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TimeSpan> _delays = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Exception> _failures = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private int _openPages;

    public FakeBrowser()
    {
        Browser = Substitute.For<IBrowser>();
        Browser.NewPageAsync().Returns(_ => Task.FromResult(CreatePage()));
    }

    public IBrowser Browser { get; }

    /// <summary>Every URL navigated to, in navigation order.</summary>
    public ConcurrentQueue<string> Visited { get; } = new();

    /// <summary>Every path a PDF was written to.</summary>
    public ConcurrentQueue<string> PdfPaths { get; } = new();

    /// <summary>Every page opened.</summary>
    public ConcurrentQueue<IPage> Pages { get; } = new();

    /// <summary>The most pages that were open at the same time.</summary>
    public int MaxOpenPages { get; private set; }

    public FakeBrowser WithPage(string url, params string[]? links)
    {
        _links[url] = links;
        return this;
    }

    public FakeBrowser WithWidth(string url, double width)
    {
        _widths[url] = width;
        return this;
    }

    public FakeBrowser WithDelay(string url, TimeSpan delay)
    {
        _delays[url] = delay;
        return this;
    }

    public FakeBrowser WithFailure(string url, Exception? exception = null)
    {
        _failures[url] = exception ?? new NavigationException($"net::ERR_CONNECTION_REFUSED at {url}");
        return this;
    }

    private IPage CreatePage()
    {
        IPage page = Substitute.For<IPage>();
        string? current = null;

        lock (_gate)
        {
            _openPages++;
            MaxOpenPages = Math.Max(MaxOpenPages, _openPages);
        }

        page.GoToAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<WaitUntilNavigation[]>())
            .Returns(ci =>
            {
                current = ci.ArgAt<string>(0);
                return GoTo(current);
            });

        page.EvaluateExpressionAsync<string[]>(Arg.Any<string>())
            // A null entry deliberately makes the page return null, as EvaluateExpressionAsync can.
            .Returns(_ => Task.FromResult(current is not null && _links.TryGetValue(current, out string[]? links) ? links! : []));

        page.PdfAsync(Arg.Any<string>())
            .Returns(ci =>
            {
                string path = ci.ArgAt<string>(0);
                PdfPaths.Enqueue(path);
                TestPdf.Create(path, _widths.GetValueOrDefault(current ?? "", DefaultWidth));
                return Task.CompletedTask;
            });

        page.DisposeAsync().Returns(_ =>
        {
            lock (_gate)
            {
                _openPages--;
            }
            return ValueTask.CompletedTask;
        });

        Pages.Enqueue(page);
        return page;
    }

    private async Task<IResponse> GoTo(string url)
    {
        Visited.Enqueue(url);

        if (_delays.TryGetValue(url, out TimeSpan delay))
        {
            await Task.Delay(delay);
        }

        if (_failures.TryGetValue(url, out Exception? failure))
        {
            throw failure;
        }

        return Substitute.For<IResponse>();
    }
}
