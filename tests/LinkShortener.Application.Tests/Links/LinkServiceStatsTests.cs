using LinkShortener.Application.Links;

namespace LinkShortener.Application.Tests.Links;

public sealed class LinkServiceStatsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public async Task Statistics_returns_the_accumulated_count(long clickCount)
    {
        var repository = new FakeLinkRepository(
            new LinkStatistics("AB23CD4", clickCount, DateTime.UtcNow.AddDays(-1)));
        var service = CreateService(repository);

        var statistics = await service.GetStatisticsAsync("ab23cd4");

        Assert.Equal(clickCount, statistics.ClickCount);
        Assert.NotNull(statistics.ExpiresAtUtc);
    }

    [Fact]
    public async Task Statistics_rejects_an_unknown_code()
    {
        var service = CreateService(new FakeLinkRepository(null));

        await Assert.ThrowsAsync<LinkNotFoundException>(
            () => service.GetStatisticsAsync("AB23CD4"));
    }

    private static LinkService CreateService(FakeLinkRepository repository) =>
        new(repository, new UnusedCodeGenerator(), TimeProvider.System);

    private sealed class UnusedCodeGenerator : IShortCodeGenerator
    {
        public string Generate() => throw new InvalidOperationException("Code generation is not used by these tests.");
    }

    private sealed class FakeLinkRepository(LinkStatistics? statistics) : ILinkRepository
    {
        public Task<bool> CodeExistsAsync(string canonicalCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(statistics?.Code == canonicalCode);

        public Task<bool> TryAddAsync(ShortLink link, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LinkDestination?> GetDestinationAsync(
            string canonicalCode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TryIncrementClickCountAsync(
            string canonicalCode,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LinkStatistics?> GetStatisticsAsync(
            string canonicalCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(statistics?.Code == canonicalCode ? statistics : null);
    }
}
