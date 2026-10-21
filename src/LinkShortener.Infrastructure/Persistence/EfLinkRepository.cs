using LinkShortener.Application.Links;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Infrastructure.Persistence;

public sealed class EfLinkRepository(
    LinkDbContext dbContext,
    SqliteTransientRetryPolicy retryPolicy) : ILinkRepository
{
    public Task<bool> CodeExistsAsync(string canonicalCode, CancellationToken cancellationToken = default) =>
        dbContext.Links.AnyAsync(link => link.Code == canonicalCode, cancellationToken);

    public async Task<bool> TryAddAsync(ShortLink link, CancellationToken cancellationToken = default)
    {
        dbContext.Links.Add(link);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(link).State = EntityState.Detached;
            return false;
        }
    }

    public Task<LinkDestination?> GetDestinationAsync(
        string canonicalCode,
        CancellationToken cancellationToken = default) =>
        dbContext.Links
            .AsNoTracking()
            .Where(link => link.Code == canonicalCode)
            .Select(link => new LinkDestination(
                link.Id,
                link.Code,
                link.DestinationUrl,
                link.ExpiresAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> TryIncrementClickCountAsync(
        string canonicalCode,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await retryPolicy.ExecuteAsync(
            token => dbContext.Links
                .Where(link =>
                    link.Code == canonicalCode &&
                    (link.ExpiresAtUtc == null || link.ExpiresAtUtc > utcNow) &&
                    link.ClickCount < long.MaxValue)
                .ExecuteUpdateAsync(
                    updates => updates.SetProperty(link => link.ClickCount, link => link.ClickCount + 1),
                    token),
            cancellationToken);

        return affectedRows == 1;
    }

    public Task<LinkStatistics?> GetStatisticsAsync(
        string canonicalCode,
        CancellationToken cancellationToken = default) =>
        dbContext.Links
            .AsNoTracking()
            .Where(link => link.Code == canonicalCode)
            .Select(link => new LinkStatistics(link.Code, link.ClickCount, link.ExpiresAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        };
}
