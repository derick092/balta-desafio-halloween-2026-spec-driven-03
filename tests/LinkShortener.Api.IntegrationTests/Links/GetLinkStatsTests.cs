using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinkShortener.Application.Links;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinkShortener.Api.IntegrationTests.Links;

public sealed class GetLinkStatsTests : IDisposable
{
    private readonly LinkApiFactory _factory = new();
    private readonly HttpClient _client;

    public GetLinkStatsTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Statistics_are_public_and_return_zero_for_a_new_link()
    {
        const string code = "STATS27";
        var create = await _client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://example.com/stats", code });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var response = await _client.GetAsync($"/api/links/{code}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("clickCount").GetInt64());
    }

    [Fact]
    public async Task Statistics_remain_available_after_expiration()
    {
        const string code = "EXPSTAT";
        await SeedLinkAsync(new ShortLink(
            Guid.NewGuid(),
            code,
            "https://example.com/expired",
            DateTime.UtcNow.AddMinutes(-1),
            12));

        var response = await _client.GetAsync($"/api/links/{code}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(12, body.RootElement.GetProperty("clickCount").GetInt64());
    }

    [Fact]
    public async Task Statistics_return_not_found_for_an_unknown_code()
    {
        var response = await _client.GetAsync("/api/links/NOEXIST/stats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task SeedLinkAsync(ShortLink link)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LinkDbContext>();
        dbContext.Links.Add(link);
        await dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
