namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados internos do pedido de reset de password.</summary>
public enum PasswordResetRequestStoreStatus
{
    Issued,
    NotEligible,
    ConcurrencyConflict
}
