using Application.Features.Authentication.Abstractions;

namespace Application.Features.Authentication.Dtos;

/// <summary>Sessão emitida pela Application sem semântica HTTP.</summary>
public sealed record AuthenticationSessionDto(
    Guid UserId,
    Guid? TrainerId,
    string Role,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RawRefreshToken,
    string RawCsrfToken,
    DateTime RefreshTokenExpiresAt)
{
    /// <summary>Compõe o DTO exclusivamente a partir de contratos validados.</summary>
    public static AuthenticationSessionDto Create(
        AuthenticatedPrincipal principal,
        IssuedAccessToken accessToken,
        IssuedRefreshSession issuedRefreshSession)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(accessToken);
        ArgumentNullException.ThrowIfNull(issuedRefreshSession);

        return new(
            principal.UserId,
            principal.TrainerId,
            principal.Role,
            accessToken.Token,
            accessToken.ExpiresAt,
            issuedRefreshSession.RawToken,
            issuedRefreshSession.RawCsrfToken,
            issuedRefreshSession.ExpiresAt);
    }
}
