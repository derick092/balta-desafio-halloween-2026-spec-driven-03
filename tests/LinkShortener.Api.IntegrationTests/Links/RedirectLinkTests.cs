using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using LinkShortener.Application.Links;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinkShortener.Api.IntegrationTests.Links;

public sealed class RedirectLinkTests : IDisposable
{
    private readonly LinkApiFactory _factory = new();
    private readonly HttpClient _client;

    public RedirectLinkTests()
    {
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Redirect_counts_an_active_access_and_returns_location()
    {
        var code = CreateCode("ACT");
        var created = await _client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://example.com/redirect", code });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var redirect = await _client.GetAsync($"/{code}");

        Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
        Assert.Equal("https://example.com/redirect", redirect.Headers.Location?.ToString());
        Assert.Equal(1, await ReadClickCountAsync(code));
    }

    [Fact]
    public async Task Redirect_returns_gone_for_expired_links_without_counting_them()
    {
        const string code = "EXPSTAT";
        await SeedLinkAsync(new ShortLink(
            Guid.NewGuid(),
            code,
            "https://example.com/expired",
            DateTime.UtcNow.AddMinutes(-1),
            5));

        var redirect = await _client.GetAsync($"/{code}");

        Assert.Equal(HttpStatusCode.Gone, redirect.StatusCode);
        Assert.Equal(5, await ReadClickCountAsync(code));
    }

    [Fact]
    public async Task Redirect_returns_not_found_for_unknown_codes()
    {
        const string code = "UNKN234";

        var redirect = await _client.GetAsync($"/{code}");

        Assert.Equal(HttpStatusCode.NotFound, redirect.StatusCode);
    }

    [Fact]
    public async Task Concurrent_redirects_count_every_successful_request()
    {
        const int requestCount = 10;
        var code = CreateCode("CNT");
        var created = await _client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://example.com/concurrent", code });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, requestCount)
                .Select(_ => _client.GetAsync($"/{code}")));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Found, response.StatusCode));
        Assert.Equal(requestCount, await ReadClickCountAsync(code));
    }

    private async Task<long> ReadClickCountAsync(string code)
    {
        var response = await _client.GetAsync($"/api/links/{code}/stats");
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("clickCount").GetInt64();
    }

    private async Task SeedLinkAsync(ShortLink link)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LinkDbContext>();
        dbContext.Links.Add(link);
        await dbContext.SaveChangesAsync();
    }

    private static string CreateCode(string prefix)
    {
        const string alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        var suffix = new string(
            RandomNumberGenerator.GetBytes(7 - prefix.Length)
                .Select(value => alphabet[value % alphabet.Length])
                .ToArray());
        return prefix + suffix;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
