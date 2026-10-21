using LinkShortener.Application.Links;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LinkShortener.Api.Configuration;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            LinkValidationException validation =>
                (StatusCodes.Status400BadRequest, "Solicitação inválida", validation.Message),
            LinkCodeConflictException conflict =>
                (StatusCodes.Status409Conflict, "Código indisponível", conflict.Message),
            LinkNotFoundException notFound =>
                (StatusCodes.Status404NotFound, "Link não encontrado", notFound.Message),
            LinkExpiredException expired =>
                (StatusCodes.Status410Gone, "Link expirado", expired.Message),
            LinkCodeGenerationException or LinkCounterExhaustedException or LinkPersistenceUnavailableException =>
                (StatusCodes.Status503ServiceUnavailable, "Serviço temporariamente indisponível", exception.Message),
            _ =>
                (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado.")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Falha ao processar a solicitação.");
        }

        httpContext.Response.StatusCode = statusCode;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
