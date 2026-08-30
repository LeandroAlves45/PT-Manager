namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados persistentes possíveis do logout.</summary>
public enum RevokeSessionStoreStatus
{
    Revoked,
    NotFound,
    CsrfInvalid
}
