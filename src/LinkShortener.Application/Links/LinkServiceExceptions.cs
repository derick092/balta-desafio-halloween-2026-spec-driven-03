namespace LinkShortener.Application.Links;

public sealed class LinkValidationException(string message) : Exception(message);

public sealed class LinkCodeConflictException()
    : Exception("O código escolhido já está em uso.");

public sealed class LinkCodeGenerationException()
    : Exception("Não foi possível gerar um código disponível. Tente novamente.");

public sealed class LinkNotFoundException()
    : Exception("O link solicitado não foi encontrado.");

public sealed class LinkExpiredException()
    : Exception("Este link expirou e não pode mais redirecionar.");

public sealed class LinkCounterExhaustedException()
    : Exception("O contador de acessos atingiu seu limite e não pode ser incrementado.");

public sealed class LinkPersistenceUnavailableException(Exception innerException)
    : Exception("Não foi possível registrar o acesso devido à indisponibilidade temporária do armazenamento.", innerException);
