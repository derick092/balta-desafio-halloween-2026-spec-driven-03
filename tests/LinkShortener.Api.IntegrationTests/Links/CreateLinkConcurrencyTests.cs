using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LinkShortener.Api.IntegrationTests.Links;

public sealed class CreateLinkConcurrencyTests : IDisposable
{
    private readonly LinkApiFactory _factory = new();
    private readonly HttpClient _client;

    public CreateLinkConcurrencyTests()
    {
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Concurrent_requests_for_the_same_code_create_only_one_link()
    {
        const string code = "RACE27";
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 2)
                .Select(index => _client.PostAsJsonAsync(
                    "/api/links",
                    new
                    {
                        destinationUrl = $"https://example.com/race/{index}",
                        code
                    })));

        var winner = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        using var body = JsonDocument.Parse(await winner.Content.ReadAsStringAsync());
        var destination = body.RootElement.GetProperty("destinationUrl").GetString();
        var redirect = await _client.GetAsync($"/{code}");
        Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
        Assert.Equal(destination, redirect.Headers.Location?.ToString());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
