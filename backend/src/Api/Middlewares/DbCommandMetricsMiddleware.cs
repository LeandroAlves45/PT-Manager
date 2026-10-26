using Application.Common.Observability;
using Infrastructure.Observability;

namespace Api.Middlewares;

/// <summary>Regista quantos comandos SQL cada pedido executou, por rota.</summary>
/// <remarks>
/// Alimenta <c>ptm.http.db_commands</c>: o orçamento de ≤ 5 queries por leitura (Gate 7A) e
/// a procura de leituras repetitivas que o Gate 7B avalia para cache. A tag é o template
/// da rota (<c>api/v1/clients/{id}</c>), nunca o path com ids. Pedidos sem endpoint (404)
/// não entram, para não abrir uma série por URL inventado.
/// </remarks>
///
public sealed class DbCommandMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public DbCommandMetricsMiddleware(RequestDelegate next) =>
        _next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(HttpContext httpContext)
    {
        var commands = RequestDbCommandCounter.Start();
        try
        {
            await _next(httpContext);
        }
        finally
        {
            if (httpContext.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { } route })
            {
                PtManagerTelemetry.RequestDbCommands.Record(
                    commands.Value,
                    new KeyValuePair<string, object?>("http.route", route),
                    new KeyValuePair<string, object?>("http.request.method", httpContext.Request.Method));
            }
        }
    }
}
