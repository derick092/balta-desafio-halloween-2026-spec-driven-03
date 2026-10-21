namespace LinkShortener.Application.Links;

public sealed record ShortLink(
    Guid Id,
    string Code,
    string DestinationUrl,
    DateTime? ExpiresAtUtc,
    long ClickCount);

public sealed record LinkDestination(
    Guid Id,
    string Code,
    string DestinationUrl,
    DateTime? ExpiresAtUtc);

public sealed record LinkStatistics(
    string Code,
    long ClickCount,
    DateTime? ExpiresAtUtc);

public sealed record CreateLinkCommand(
    string DestinationUrl,
    string? Code = null,
    DateTimeOffset? ExpiresAt = null);
