using Api.Configuration;
using Api.Middlewares;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Api.Security;

/// <summary>Recusa pedidos cujo header Origin não pertence à allowlist do ambiente.</summary>
public sealed class RequireOriginFilter : IAsyncAuthorizationFilter
{
    private readonly ApiCorsOptions _corsOptions;
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<RequireOriginFilter> _logger;

    public RequireOriginFilter(
        IOptions<ApiCorsOptions> corsOptions,
        IProblemDetailsService problemDetailsService,
        ILogger<RequireOriginFilter> logger)
    {
        _corsOptions = corsOptions.Value ?? throw new ArgumentNullException(nameof(corsOptions));
        _problemDetailsService = problemDetailsService ?? throw new ArgumentNullException(nameof(problemDetailsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<RequireOriginAttribute>() is null)
            return;

        if (IsAllowed(context.HttpContext.Request.Headers.Origin))
            return;

        _logger.LogWarning(
            "A cookie-authorized request was rejected because its origin is not allowed.");

        await ProblemDetailsResponseWriter.WriteAsync(
            context.HttpContext,
            _problemDetailsService,
            StatusCodes.Status403Forbidden,
            "Forbidden",
            "The request origin is not allowed.");

        context.Result = new EmptyResult();
    }

    private bool IsAllowed(StringValues origin)
    {
        if (origin.Count != 1)
            return false;

        var value = origin[0];
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
            return false;

        // A comparação é feita sobre a autoridade normalizada, para que
        // "https://app.pt" e "https://app.pt:443" não sejam tratados como
        // origens diferentes.
        var normalized = parsed.GetLeftPart(UriPartial.Authority);

        foreach (var allowed in _corsOptions.AllowedOrigins)
        {
            if (Uri.TryCreate(allowed, UriKind.Absolute, out var allowedUri) &&
                string.Equals(
                    allowedUri.GetLeftPart(UriPartial.Authority),
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
