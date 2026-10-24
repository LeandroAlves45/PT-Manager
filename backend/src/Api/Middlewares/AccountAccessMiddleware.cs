using Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Net.Http.Headers;

namespace Api.Middlewares;

/// <summary>
/// Corta uma sessão já aberta no pedido seguinte quando a conta deixou de poder usar a app
/// (cliente arquivado, conta inativa, personal trainer bloqueado, fim dos dias de acesso dos
/// clientes de um bloqueado). O token continuaria válido até expirar; esta leitura, uma
/// consulta indexada por pedido autenticado, é o que torna o corte imediato. O arquivado e a
/// conta inativa recebem 401 <c>authentication_session_ended</c>, sem explicação; o personal
/// trainer bloqueado 403 <c>trainer_account_blocked</c> com o motivo e a nota; o cliente fora
/// do prazo 401 <c>client_access_period_ended</c>, sem motivo nem nota.
/// </summary>
public sealed class AccountAccessMiddleware
{
    internal const string SessionEndedCode = "authentication_session_ended";
    internal const string TrainerBlockedCode = "trainer_account_blocked";
    internal const string ClientAccessPeriodEndedCode = "client_access_period_ended";

    private readonly RequestDelegate _next;

    public AccountAccessMiddleware(RequestDelegate next) =>
        _next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(
        HttpContext httpContext,
        ITenantContext tenantContext,
        IAccountAccessQueries accessQueries,
        IClock clock,
        IProblemDetailsService problemDetailsService)
    {
        // Endpoints anónimos (entrar, renovar, sair) não dependem do estado da conta.
        var endpoint = httpContext.GetEndpoint();
        if (httpContext.User.Identity?.IsAuthenticated is not true ||
            endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null ||
            tenantContext.UserId is not { } userId ||
            tenantContext.Role is not { } role)
        {
            await _next(httpContext);
            return;
        }

        var access = await accessQueries.GetAsync(
            userId,
            role,
            tenantContext.TrainerId,
            clock.UtcNow,
            httpContext.RequestAborted);

        if (access.Status == AccountAccessStatus.Allowed)
        {
            await _next(httpContext);
            return;
        }

        if (access.Status == AccountAccessStatus.TrainerBlocked)
        {
            // O personal trainer bloqueado vê o motivo e a nota, também a meio de uma sessão.
            await ProblemDetailsResponseWriter.WriteAsync(
                httpContext,
                problemDetailsService,
                StatusCodes.Status403Forbidden,
                TrainerBlockedCode,
                "The personal trainer account is blocked.",
                BlockExtensions(access));
            return;
        }

        httpContext.Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer";
        await ProblemDetailsResponseWriter.WriteAsync(
            httpContext,
            problemDetailsService,
            StatusCodes.Status401Unauthorized,
            access.Status == AccountAccessStatus.ClientAccessPeriodEnded
                ? ClientAccessPeriodEndedCode
                : SessionEndedCode,
            access.Status == AccountAccessStatus.ClientAccessPeriodEnded
                ? "The access period has ended."
                : "The session is no longer valid.");
    }

    /// <summary>Motivo e nota do bloqueio, nas extensões do ProblemDetails.</summary>
    internal static IReadOnlyDictionary<string, object?> BlockExtensions(AccountAccessState access) =>
        new Dictionary<string, object?>
        {
            ["block_reason"] = access.BlockReason,
            ["block_note"] = access.BlockNote
        };
}
