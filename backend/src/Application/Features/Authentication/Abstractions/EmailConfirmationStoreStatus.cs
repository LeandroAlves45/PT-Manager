namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados esperados da emissão e consumo de confirmação.</summary>
public enum EmailConfirmationStoreStatus
{
    Issued,
    Confirmed,
    UserNotFound,
    AlreadyConfirmed,
    AccountInactive,
    TokenNotFound,
    TokenExpired,
    TokenAlreadyConsumed,
    ConcurrencyConflict,

    /// <summary>Já existe uma confirmação emitida há menos do que o intervalo mínimo; nada foi alterado.</summary>
    RecentlyIssued
}
