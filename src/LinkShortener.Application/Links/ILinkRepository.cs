namespace LinkShortener.Application.Links;

public interface ILinkRepository
{
    Task<bool> CodeExistsAsync(string canonicalCode, CancellationToken cancellationToken = default);

    Task<bool> TryAddAsync(ShortLink link, CancellationToken cancellationToken = default);

    Task<LinkDestination?> GetDestinationAsync(
        string canonicalCode,
        CancellationToken cancellationToken = default);

    Task<bool> TryIncrementClickCountAsync(
        string canonicalCode,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<LinkStatistics?> GetStatisticsAsync(
        string canonicalCode,
        CancellationToken cancellationToken = default);
}
