using LinkShortener.Application.Links;

namespace LinkShortener.Application.Tests.Links;

public sealed class LinkServiceCreateTests
{
    [Fact]
    public async Task Create_without_code_generates_seven_character_non_ambiguous_code()
    {
        var repository = new FakeLinkRepository();
        var generator = new SequenceCodeGenerator("AB23CD4");
        var service = new LinkService(repository, generator, TimeProvider.System);

        var link = await service.CreateAsync(new CreateLinkCommand("https://example.com/path"));

        Assert.Equal("AB23CD4", link.Code);
        Assert.Equal(7, link.Code.Length);
        Assert.Equal(0, link.ClickCount);
        Assert.Equal(link, repository.Links.Single());
    }

    [Theory]
    [InlineData("AB23")]
    [InlineData("ABCDEFGHJKMNPQRSTUVW")]
    public async Task Create_accepts_custom_codes_at_length_boundaries(string code)
    {
        var service = CreateService(new FakeLinkRepository());

        var link = await service.CreateAsync(new CreateLinkCommand("https://example.com", code));

        Assert.Equal(code, link.Code);
    }

    [Fact]
    public async Task Create_normalizes_custom_code_to_uppercase()
    {
        var service = CreateService(new FakeLinkRepository());

        var link = await service.CreateAsync(new CreateLinkCommand("https://example.com", "ab23"));

        Assert.Equal("AB23", link.Code);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("not a URL")]
    [InlineData("https://")]
    public async Task Create_rejects_invalid_destination_urls(string destination)
    {
        var service = CreateService(new FakeLinkRepository());

        await Assert.ThrowsAsync<LinkValidationException>(
            () => service.CreateAsync(new CreateLinkCommand(destination)));
    }

    [Fact]
    public async Task Create_accepts_destination_of_exactly_2048_characters()
    {
        var destination = "https://example.com/" + new string('a', 2048 - "https://example.com/".Length);
        var service = CreateService(new FakeLinkRepository());

        var link = await service.CreateAsync(new CreateLinkCommand(destination));

        Assert.Equal(2048, link.DestinationUrl.Length);
    }

    [Fact]
    public async Task Create_rejects_destination_longer_than_2048_characters()
    {
        var destination = "https://example.com/" + new string('a', 2049 - "https://example.com/".Length);
        var service = CreateService(new FakeLinkRepository());

        await Assert.ThrowsAsync<LinkValidationException>(
            () => service.CreateAsync(new CreateLinkCommand(destination)));
    }

    [Theory]
    [InlineData("AB2")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZA")]
    [InlineData("AB0C")]
    [InlineData("AB1C")]
    [InlineData("ABIC")]
    [InlineData("ABOC")]
    [InlineData("ABiC")]
    [InlineData("ABlC")]
    [InlineData("ABoC")]
    [InlineData("AB-C")]
    public async Task Create_rejects_custom_codes_outside_allowed_alphabet_or_length(string code)
    {
        var service = CreateService(new FakeLinkRepository());

        await Assert.ThrowsAsync<LinkValidationException>(
            () => service.CreateAsync(new CreateLinkCommand("https://example.com", code)));
    }

    [Fact]
    public async Task Create_rejects_expiration_in_the_past()
    {
        var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        var service = new LinkService(new FakeLinkRepository(), new SequenceCodeGenerator("AB23CD4"), new TestTimeProvider(now));

        await Assert.ThrowsAsync<LinkValidationException>(
            () => service.CreateAsync(new CreateLinkCommand("https://example.com", ExpiresAt: now.AddSeconds(-1))));
    }

    [Fact]
    public async Task Create_rejects_duplicate_custom_codes()
    {
        var repository = new FakeLinkRepository();
        var service = CreateService(repository);
        await service.CreateAsync(new CreateLinkCommand("https://example.com", "AB23"));

        await Assert.ThrowsAsync<LinkCodeConflictException>(
            () => service.CreateAsync(new CreateLinkCommand("https://other.example", "ab23")));
        Assert.Single(repository.Links);
    }

    [Fact]
    public async Task Create_retries_generated_code_collision()
    {
        var repository = new FakeLinkRepository();
        repository.ExistingCodes.Add("AB23CD4");
        var service = CreateService(repository, new SequenceCodeGenerator("AB23CD4", "EF56GH7"));

        var link = await service.CreateAsync(new CreateLinkCommand("https://example.com"));

        Assert.Equal("EF56GH7", link.Code);
        Assert.Single(repository.Links);
    }

    [Fact]
    public async Task Create_retries_database_code_collision_before_persisting_generated_code()
    {
        var repository = new FakeLinkRepository { CollisionsOnNextInsert = 1 };
        var service = CreateService(repository, new SequenceCodeGenerator("AB23CD4", "EF56GH7"));

        var link = await service.CreateAsync(new CreateLinkCommand("https://example.com"));

        Assert.Equal("EF56GH7", link.Code);
        Assert.Single(repository.Links);
    }

    private static LinkService CreateService(
        FakeLinkRepository repository,
        SequenceCodeGenerator? generator = null) =>
        new(repository, generator ?? new SequenceCodeGenerator("AB23CD4"), TimeProvider.System);

    private sealed class SequenceCodeGenerator(params string[] codes) : IShortCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);

        public string Generate() => _codes.Dequeue();
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeLinkRepository : ILinkRepository
    {
        public List<ShortLink> Links { get; } = [];
        public HashSet<string> ExistingCodes { get; } = new(StringComparer.Ordinal);
        public int CollisionsOnNextInsert { get; set; }

        public Task<bool> CodeExistsAsync(string canonicalCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingCodes.Contains(canonicalCode) || Links.Any(link => link.Code == canonicalCode));

        public Task<bool> TryAddAsync(ShortLink link, CancellationToken cancellationToken = default)
        {
            if (CollisionsOnNextInsert > 0)
            {
                CollisionsOnNextInsert--;
                return Task.FromResult(false);
            }

            if (Links.Any(existing => existing.Code == link.Code))
            {
                return Task.FromResult(false);
            }

            Links.Add(link);
            return Task.FromResult(true);
        }

        public Task<LinkDestination?> GetDestinationAsync(string canonicalCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Links.Where(link => link.Code == canonicalCode)
                .Select(link => new LinkDestination(link.Id, link.Code, link.DestinationUrl, link.ExpiresAtUtc))
                .SingleOrDefault());

        public Task<bool> TryIncrementClickCountAsync(
            string canonicalCode,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var link = Links.SingleOrDefault(item => item.Code == canonicalCode);
            if (link is null || (link.ExpiresAtUtc is not null && link.ExpiresAtUtc <= utcNow) || link.ClickCount == long.MaxValue)
            {
                return Task.FromResult(false);
            }

            var linkIndex = Links.IndexOf(link);
            Links[linkIndex] = link with { ClickCount = link.ClickCount + 1 };
            return Task.FromResult(true);
        }

        public Task<LinkStatistics?> GetStatisticsAsync(string canonicalCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Links.Where(link => link.Code == canonicalCode)
                .Select(link => new LinkStatistics(link.Code, link.ClickCount, link.ExpiresAtUtc))
                .SingleOrDefault());
    }
}
