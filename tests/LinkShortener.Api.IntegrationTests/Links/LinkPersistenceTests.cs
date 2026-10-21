using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LinkShortener.Api.IntegrationTests.Links;

public sealed class LinkPersistenceTests
{
    [Fact]
    public async Task Link_and_click_count_survive_recreating_the_api_host()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"link-shortener-restart-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(directory, "links.db");

        try
        {
            string code;
            using (var firstFactory = LinkApiFactory.ForDatabase(databasePath))
            using (var firstClient = firstFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            }))
            {
                var create = await firstClient.PostAsJsonAsync(
                    "/api/links",
                    new { destinationUrl = "https://example.com/persistent", code = "PERSST7" });
                Assert.Equal(HttpStatusCode.Created, create.StatusCode);
                using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
                code = created.RootElement.GetProperty("code").GetString()!;

                var redirect = await firstClient.GetAsync($"/{code}");
                Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
            }

            using (var secondFactory = LinkApiFactory.ForDatabase(databasePath))
            using (var secondClient = secondFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            }))
            {
                var stats = await secondClient.GetAsync($"/api/links/{code}/stats");
                Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
                using var body = JsonDocument.Parse(await stats.Content.ReadAsStringAsync());
                Assert.Equal(1, body.RootElement.GetProperty("clickCount").GetInt64());

                var redirect = await secondClient.GetAsync($"/{code}");
                Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
                Assert.Equal("https://example.com/persistent", redirect.Headers.Location?.ToString());

                var updatedStats = await secondClient.GetAsync($"/api/links/{code}/stats");
                using var updatedBody = JsonDocument.Parse(await updatedStats.Content.ReadAsStringAsync());
                Assert.Equal(2, updatedBody.RootElement.GetProperty("clickCount").GetInt64());
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
