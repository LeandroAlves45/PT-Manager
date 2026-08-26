namespace Application.Features.Authentication.Abstractions;

/// <summary>Resultado da rotation sem expor hash persistido.</summary>
public sealed record RotateRefreshStoreResult(
    RotateRefreshStoreStatus Kind,
    AuthenticatedPrincipal? Principal,
    IssuedRefreshSession? RefreshSession
)
{
    public static RotateRefreshStoreResult Rotated(
        AuthenticatedPrincipal principal,
        IssuedRefreshSession refreshSession)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(refreshSession);

        return new(
            RotateRefreshStoreStatus.Rotated,
            principal,
            refreshSession
        );
    }

    public static RotateRefreshStoreResult Failure(
        RotateRefreshStoreStatus status)
    {
        if (status == RotateRefreshStoreStatus.Rotated)
            throw new ArgumentOutOfRangeException(nameof(status));

        return new(status, null, null);
    }
}
