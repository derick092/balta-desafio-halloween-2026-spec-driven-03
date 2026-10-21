using LinkShortener.Api.Endpoints;
using Microsoft.Extensions.Options;

namespace LinkShortener.Api.Configuration;

public sealed class LinkShortenerOptions
{
    public const string SectionName = "ShortLinks";

    public string PublicOrigin { get; set; } = string.Empty;
}

public static class ApiConfiguration
{
    public static IServiceCollection AddLinkShortenerOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<LinkShortenerOptions>()
            .Bind(configuration.GetSection(LinkShortenerOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.PublicOrigin, UriKind.Absolute, out var origin) &&
                    (origin.Scheme == Uri.UriSchemeHttp || origin.Scheme == Uri.UriSchemeHttps),
                "ShortLinks:PublicOrigin deve ser uma URL absoluta HTTP ou HTTPS.")
            .ValidateOnStart();

        return services;
    }

    public static WebApplication MapLinkShortenerEndpoints(this WebApplication app)
    {
        app.MapLinkEndpoints();
        return app;
    }
}
