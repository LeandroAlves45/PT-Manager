using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Authentication.Abstractions;

namespace Application.Features.Authentication;

/// <summary>
/// Última verificação de uma entrada já com a credencial validada (palavra-passe ou Google):
/// a conta pode mesmo usar a app? É a mesma leitura que corta as sessões abertas a cada
/// pedido. Quando não pode, a sessão acabada de criar é revogada.
/// </summary>
internal static class AccountAccessGate
{
    internal static async Task<Error?> CheckAsync(
        IAccountAccessQueries accessQueries,
        IAuthenticationSessionStore sessions,
        AuthenticatedPrincipal principal,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var access = await accessQueries.GetAsync(
            principal.UserId,
            principal.Role,
            principal.TrainerId,
            now,
            cancellationToken);
        if (access.Status == AccountAccessStatus.Allowed)
            return null;

        await sessions.RevokeAllAsync(principal.UserId, now, cancellationToken);
        return access.Status switch
        {
            AccountAccessStatus.TrainerBlocked =>
                AuthenticationErrors.TrainerBlocked(access.BlockReason, access.BlockNote),
            AccountAccessStatus.ClientAccessPeriodEnded => AuthenticationErrors.ClientAccessPeriodEnded,
            _ => AuthenticationErrors.InvalidCredentials
        };
    }
}
