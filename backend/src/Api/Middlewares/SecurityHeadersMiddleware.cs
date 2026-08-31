using Api.Configuration;
using Scalar.AspNetCore;

namespace Api.Middlewares;

/// <summary>Adiciona headers defensivos adequados a uma API JSON.</summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) =>
        _next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(HttpContext httpContext)
    {
        httpContext.Response.OnStarting(() =>
        {
            var headers = httpContext.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy = BuildContentSecurityPolicy(httpContext);
            headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

            if (httpContext.GetEndpoint()?.Metadata.GetMetadata<SensitiveResponseAttribute>() is not null)
            {
                headers.CacheControl = "no-store";
                headers.Pragma = "no-cache";
            }

            return Task.CompletedTask;
        });

        await _next(httpContext);
    }

    private static string BuildContentSecurityPolicy(HttpContext httpContext)
    {
        if (!httpContext.Items.TryGetValue(
                ScalarOptions.NonceHttpContextItemKey,
                out var nonceValue)
            || nonceValue is not string nonce)
        {
            return "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
        }

        // A UI Scalar precisa de executar o bundle e estilos no browser. O nonce
        // por pedido mantém os scripts inline bloqueados fora desta resposta.
        return $"default-src 'none'; "
            + $"script-src 'nonce-{nonce}' https://cdn.jsdelivr.net; "
            + "style-src 'self' 'unsafe-inline'; "
            + "img-src 'self' data: blob:; "
            + "font-src 'self' data:; "
            + "connect-src 'self'; "
            + "frame-ancestors 'none'; base-uri 'none'";
    }
}
