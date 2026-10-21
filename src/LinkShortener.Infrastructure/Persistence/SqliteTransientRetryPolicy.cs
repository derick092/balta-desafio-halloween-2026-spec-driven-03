using LinkShortener.Application.Links;
using Microsoft.Data.Sqlite;

namespace LinkShortener.Infrastructure.Persistence;

public sealed class SqliteTransientRetryPolicy
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(25),
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100)
    ];

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (SqliteException exception) when (
                IsBusyOrLocked(exception) &&
                attempt < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
            catch (SqliteException exception) when (IsBusyOrLocked(exception))
            {
                throw new LinkPersistenceUnavailableException(exception);
            }
        }
    }

    private static bool IsBusyOrLocked(SqliteException exception) =>
        exception.SqliteErrorCode is 5 or 6;
}
