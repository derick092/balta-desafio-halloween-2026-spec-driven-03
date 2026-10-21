using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinkShortener.Api.IntegrationTests.Links;

public sealed class CreateLinkTests : IDisposable
{
    private readonly LinkApiFactory _factory = new();
    private readonly HttpClient _client;

    public CreateLinkTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Create_returns_persisted_short_link()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://example.com/a/long/path" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        var code = root.GetProperty("code").GetString();
        Assert.NotNull(code);
        Assert.Equal(7, code.Length);
        Assert.Matches("^[A-HJ-KM-NP-Z2-9]{7}$", code);
        Assert.Equal("https://example.com/a/long/path", root.GetProperty("destinationUrl").GetString());
        Assert.StartsWith("http://localhost/", root.GetProperty("shortUrl").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("expiresAt").ValueKind);
    }

    [Fact]
    public async Task Create_returns_bad_request_problem_details_for_invalid_input()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "ftp://example.com", code = "AB0C" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("inválid", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_returns_conflict_for_duplicate_custom_code()
    {
        var first = await _client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://example.com/first", code = "SAVE27" });
        var second = await _client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://example.com/second", code = "save27" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
