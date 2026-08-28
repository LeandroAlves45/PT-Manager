using Microsoft.AspNetCore.Mvc;

namespace Api.Middlewares;

/// <summary>
/// Escreve uma resposta de detalhes de problema (ProblemDetails) para o contexto HTTP fornecido.
/// </summary>
internal static class ProblemDetailsResponseWriter
{
    public static async Task WriteAsync(
        HttpContext httpContext,
        IProblemDetailsService problemDetailsService,
        int statusCode,
        string title,
        string detail
    )
    {
        if (httpContext.Response.HasStarted)
            return;

        httpContext.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["correlation_id"] = httpContext.TraceIdentifier;

        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        });

        if (!written)
            await httpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                httpContext.RequestAborted
            );
    }
}
