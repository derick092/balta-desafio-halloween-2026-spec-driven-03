using LinkShortener.Api.Configuration;
using LinkShortener.Application.Links;
using Microsoft.Extensions.Options;

namespace LinkShortener.Api.Endpoints;

public sealed record CreateLinkRequest(
    string DestinationUrl,
    string? Code = null,
    DateTimeOffset? ExpiresAt = null);

public sealed record CreateLinkResponse(
    string Code,
    string ShortUrl,
    string DestinationUrl,
    DateTimeOffset? ExpiresAt);

public sealed record LinkStatsResponse(
    string Code,
    long ClickCount,
    DateTimeOffset? ExpiresAt);

public static class LinkEndpoints
{
    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/links", CreateLinkAsync);
        endpoints.MapGet("/api/links/{code}/stats", GetStatisticsAsync);
        endpoints.MapGet("/{code}", RedirectAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateLinkAsync(
        CreateLinkRequest request,
        LinkService linkService,
        IOptions<LinkShortenerOptions> options,
        CancellationToken cancellationToken)
    {
        var link = await linkService.CreateAsync(
            new CreateLinkCommand(request.DestinationUrl, request.Code, request.ExpiresAt),
            cancellationToken);
        var shortUrl = $"{options.Value.PublicOrigin.TrimEnd('/')}/{link.Code}";
        var response = new CreateLinkResponse(
            link.Code,
            shortUrl,
            link.DestinationUrl,
            link.ExpiresAtUtc is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(link.ExpiresAtUtc.Value, DateTimeKind.Utc)));

        return Results.Created(shortUrl, response);
    }

    private static async Task<IResult> GetStatisticsAsync(
        string code,
        LinkService linkService,
        CancellationToken cancellationToken)
    {
        var statistics = await linkService.GetStatisticsAsync(code, cancellationToken);
        return Results.Ok(new LinkStatsResponse(
            statistics.Code,
            statistics.ClickCount,
            statistics.ExpiresAtUtc is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(statistics.ExpiresAtUtc.Value, DateTimeKind.Utc))));
    }

    private static async Task<IResult> RedirectAsync(
        string code,
        LinkService linkService,
        CancellationToken cancellationToken)
    {
        var destination = await linkService.GetRedirectDestinationAsync(code, cancellationToken);
        return Results.Redirect(destination.DestinationUrl, permanent: false, preserveMethod: false);
    }
}
