namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados esperados do consumo de convite.</summary>
public enum ConsumeClientInvitationStoreStatus
{
    Accepted,
    TokenNotFound,
    TokenExpired,
    TokenAlreadyConsumed,
    EmailMismatch,
    TransferApprovalRequired,
    RelationshipConflict,
    AccountInactive,
    ConcurrencyConflict
}
