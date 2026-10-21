using System.Globalization;

namespace LinkShortener.Application.Links;

public sealed class LinkService(
    ILinkRepository repository,
    IShortCodeGenerator codeGenerator,
    TimeProvider timeProvider)
{
    private const int GeneratedCodeAttempts = 5;

    public async Task<ShortLink> CreateAsync(
        CreateLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        var destination = ValidateDestination(command.DestinationUrl);
        var expiry = NormalizeExpiration(command.ExpiresAt);

        if (command.Code is not null)
        {
            var customCode = NormalizeAndValidateCode(command.Code);
            if (await repository.CodeExistsAsync(customCode, cancellationToken))
            {
                throw new LinkCodeConflictException();
            }

            var customLink = NewLink(customCode, destination, expiry);
            if (!await repository.TryAddAsync(customLink, cancellationToken))
            {
                throw new LinkCodeConflictException();
            }

            return customLink;
        }

        for (var attempt = 0; attempt < GeneratedCodeAttempts; attempt++)
        {
            var generatedCode = codeGenerator.Generate();
            if (!IsCanonicalCode(generatedCode) ||
                generatedCode.Length != ShortCodeGenerator.GeneratedCodeLength)
            {
                throw new InvalidOperationException("O gerador produziu um código inválido.");
            }

            if (await repository.CodeExistsAsync(generatedCode, cancellationToken))
            {
                continue;
            }

            var generatedLink = NewLink(generatedCode, destination, expiry);
            if (await repository.TryAddAsync(generatedLink, cancellationToken))
            {
                return generatedLink;
            }
        }

        throw new LinkCodeGenerationException();
    }

    public async Task<LinkDestination> GetRedirectDestinationAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var canonicalCode = NormalizeLookupCode(code);
        var destination = await repository.GetDestinationAsync(canonicalCode, cancellationToken)
            ?? throw new LinkNotFoundException();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (IsExpired(destination.ExpiresAtUtc, now))
        {
            throw new LinkExpiredException();
        }

        if (await repository.TryIncrementClickCountAsync(canonicalCode, now, cancellationToken))
        {
            return destination;
        }

        var currentDestination = await repository.GetDestinationAsync(canonicalCode, cancellationToken);
        if (currentDestination is null)
        {
            throw new LinkNotFoundException();
        }

        if (IsExpired(currentDestination.ExpiresAtUtc, timeProvider.GetUtcNow().UtcDateTime))
        {
            throw new LinkExpiredException();
        }

        throw new LinkCounterExhaustedException();
    }

    public async Task<LinkStatistics> GetStatisticsAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var canonicalCode = NormalizeLookupCode(code);
        return await repository.GetStatisticsAsync(canonicalCode, cancellationToken)
            ?? throw new LinkNotFoundException();
    }

    private ShortLink NewLink(string code, string destination, DateTime? expiry) =>
        new(Guid.NewGuid(), code, destination, expiry, 0);

    private static string ValidateDestination(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 2048 ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host))
        {
            throw new LinkValidationException("Informe uma URL absoluta HTTP ou HTTPS com até 2048 caracteres.");
        }

        return value;
    }

    private DateTime? NormalizeExpiration(DateTimeOffset? expiration)
    {
        if (expiration is null)
        {
            return null;
        }

        var utcExpiration = expiration.Value.UtcDateTime;
        if (utcExpiration <= timeProvider.GetUtcNow().UtcDateTime)
        {
            throw new LinkValidationException("A data de expiração deve estar no futuro.");
        }

        return utcExpiration;
    }

    private static string NormalizeAndValidateCode(string code)
    {
        var canonicalCode = code.ToUpperInvariant();
        if (!IsCanonicalCode(canonicalCode))
        {
            throw new LinkValidationException(
                "O código deve conter de 4 a 20 caracteres alfanuméricos não ambíguos.");
        }

        return canonicalCode;
    }

    private static string NormalizeLookupCode(string code)
    {
        var canonicalCode = code.ToUpperInvariant();
        return IsCanonicalCode(canonicalCode) ? canonicalCode : string.Empty;
    }

    private static bool IsCanonicalCode(string code)
    {
        return code.Length is >= 4 and <= 20 &&
            code.All(character => ShortCodeGenerator.Alphabet.Contains(character, StringComparison.Ordinal));
    }

    private static bool IsExpired(DateTime? expiresAtUtc, DateTime utcNow) =>
        expiresAtUtc is not null && expiresAtUtc <= utcNow;
}
