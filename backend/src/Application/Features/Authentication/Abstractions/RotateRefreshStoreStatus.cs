namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados persistentes possíveis durante refresh.</summary>
public enum RotateRefreshStoreStatus
{
    Rotated,
    NotFound,
    Expired,
    Reused,
    PrincipalInvalid,
    ConcurrencyConflict
}
