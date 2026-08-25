namespace Application.Features.Authentication.Abstractions;

/// <summary>Fronteira transacional das sessões locais.</summary>
public interface IAuthenticationSessionStore
{
    Task<AuthenticateStoreResult> AuthenticateAsync(
        string email,
        string password,
        DateTime now,
        DateTime refreshExpiresAt,
        CancellationToken cancellationToken);

    Task<RotateRefreshStoreResult> RotateAsync(
        string rawToken,
        DateTime now,
        DateTime refreshExpiresAt,
        CancellationToken cancellationToken);

    Task RevokeAsync(
        string rawToken,
        DateTime now,
        CancellationToken cancellationToken);

    Task RevokeAllAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken);
}
