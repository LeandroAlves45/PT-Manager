using Microsoft.Net.Http.Headers;

namespace Api.Middlewares;

/// <summary>Converte falhas inesperadas em Problem Details sem expor stack traces.</summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly EventId InvalidPrincipalEvent = new(1001, "InvalidPrincipal");
    private static readonly EventId UnexpectedFailureEvent = new(1002, "UnexpectedFailure");
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IProblemDetailsService problemDetailsService
    )
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _problemDetailsService = problemDetailsService ?? throw new ArgumentNullException(nameof(problemDetailsService));
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Request was cancelled by the client.");
        }
        catch (InvalidAuthenticatedPrincipalException)
        {
            _logger.LogWarning(InvalidPrincipalEvent,
                "An authenticated request contained invalid identity claims.");

            // O handler JWT escreve-o nas suas próprias rejeições,
            // mas este caminho corre depois da autenticação ter sucedido as
            // claims são válidas para o handler e inválidas para o tenant — e sem
            // esta linha a resposta sairia sem o header.
            if (!httpContext.Response.HasStarted)
                httpContext.Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer";

            await ProblemDetailsResponseWriter.WriteAsync(
                httpContext,
                _problemDetailsService,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "The authenticated identity is invalid."
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(UnexpectedFailureEvent, exception,
                "An unhandled API exception occurred.");

            await ProblemDetailsResponseWriter.WriteAsync(
                httpContext,
                _problemDetailsService,
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                "An unexpected error occurred."
            );
        }
    }
}
