namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados persistentes possíveis durante o bootstrapping de CSRF.</summary>
public enum RotateCsrfStoreStatus
{
    Rotated,
    NotFound,
    Expired,
    Reused,
    ConcurrencyConflict
}
