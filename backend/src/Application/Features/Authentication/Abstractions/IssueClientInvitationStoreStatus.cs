namespace Application.Features.Authentication.Abstractions;

/// <summary>Estados esperados da emissão de convite.</summary>
public enum IssueClientInvitationStoreStatus
{
    Issued,
    ClientNotFound,
    ClientInactive,
    EmailMismatch,
    RelationshipConflict,
    ConcurrencyConflict
}
