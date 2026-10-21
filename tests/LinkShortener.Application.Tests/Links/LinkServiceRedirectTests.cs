using LinkShortener.Application.Links;

namespace LinkShortener.Application.Tests.Links;

public sealed class LinkServiceRedirectTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Redirect_increments_an_active_link_and_returns_its_destination()
    {
        var repository = new FakeLinkRepository(NewLink());
        var service = CreateService(repository);

        var destination = await service.GetRedirectDestinationAsync("ab23cd4");

        Assert.Equal("https://example.com/path", destination.DestinationUrl);
        Assert.Equal(1, repository.ClickCount);
    }

    [Fact]
    public async Task Redirect_rejects_link_expiring_at_the_current_instant_without_incrementing()
    {
        var repository = new FakeLinkRepository(NewLink(expiresAtUtc: Now.UtcDateTime));
        var service = CreateService(repository);

        await Assert.ThrowsAsync<LinkExpiredException>(
            () => service.GetRedirectDestinationAsync("AB23CD4"));

        Assert.Equal(0, repository.ClickCount);
    }

    [Fact]
    public async Task Redirect_rejects_an_expired_link_without_incrementing()
    {
        var repository = new FakeLinkRepository(NewLink(expiresAtUtc: Now.AddSeconds(-1).UtcDateTime));
        var service = CreateService(repository);

        await Assert.ThrowsAsync<LinkExpiredException>(
            () => service.GetRedirectDestinationAsync("AB23CD4"));

        Assert.Equal(0, repository.ClickCount);
    }

    [Fact]
    public async Task Redirect_rejects_an_unknown_code()
    {
        var service = CreateService(new FakeLinkRepository());

        await Assert.ThrowsAsync<LinkNotFoundException>(
            () => service.GetRedirectDestinationAsync("AB23CD4"));
    }

    [Fact]
    public async Task Redirect_does_not_overflow_a_maximum_counter()
    {
        var repository = new FakeLinkRepository(NewLink(clickCount: long.MaxValue));
        var service = CreateService(repository);

        await Assert.ThrowsAsync<LinkCounterExhaustedException>(
            () => service.GetRedirectDestinationAsync("AB23CD4"));

        Assert.Equal(long.MaxValue, repository.ClickCount);
    }

    [Fact]
    public async Task Concurrent_redirects_preserve_every_increment()
    {
        const int requestCount = 100;
        var repository = new FakeLinkRepository(NewLink());
        var service = CreateService(repository);

        var results = await Task.WhenAll(
            Enumerable.Range(0, requestCount)
                .Select(_ => service.GetRedirectDestinationAsync("AB23CD4")));

        Assert.Equal(requestCount, results.Length);
        Assert.Equal(requestCount, repository.ClickCount);
    }

    private static LinkService CreateService(FakeLinkRepository repository) =>
        new(repository, new UnusedCodeGenerator(), new TestTimeProvider(Now));

    private static ShortLink NewLink(DateTime? expiresAtUtc = null, long clickCount = 0) =>
        new(Guid.NewGuid(), "AB23CD4", "https://example.com/path", expiresAtUtc, clickCount);

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class UnusedCodeGenerator : IShortCodeGenerator
    {
        public string Generate() => throw new InvalidOperationException("Code generation is not used by these tests.");
    }

    private sealed class FakeLinkRepository(ShortLink? initialLink = null) : ILinkRepository
    {
        private readonly object _gate = new();
        private ShortLink? _link = initialLink;

        public long ClickCount
        {
            get
            {
                lock (_gate)
                {
                    return _link?.ClickCount ?? 0;
                }
            }
        }

        public Task<bool> CodeExistsAsync(string canonicalCode, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_link?.Code == canonicalCode);
            }
        }

        public Task<bool> TryAddAsync(ShortLink link, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_link is not null)
                {
                    return Task.FromResult(false);
                }

                _link = link;
                return Task.FromResult(true);
            }
        }

        public Task<LinkDestination?> GetDestinationAsync(
            string canonicalCode,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(
                    _link?.Code == canonicalCode
                        ? new LinkDestination(_link.Id, _link.Code, _link.DestinationUrl, _link.ExpiresAtUtc)
                        : null);
            }
        }

        public Task<bool> TryIncrementClickCountAsync(
            string canonicalCode,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_link?.Code != canonicalCode ||
                    (_link.ExpiresAtUtc is not null && _link.ExpiresAtUtc <= utcNow) ||
                    _link.ClickCount == long.MaxValue)
                {
                    return Task.FromResult(false);
                }

                _link = _link with { ClickCount = _link.ClickCount + 1 };
                return Task.FromResult(true);
            }
        }

        public Task<LinkStatistics?> GetStatisticsAsync(
            string canonicalCode,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(
                    _link?.Code == canonicalCode
                        ? new LinkStatistics(_link.Code, _link.ClickCount, _link.ExpiresAtUtc)
                        : null);
            }
        }
    }
}
