namespace Application.Features.Authentication.Abstractions;

/// <summary>Gere convites de cliente e transferência de relação ativa.</summary>
public interface IClientInvitationStore
{
    Task<IssueClientInvitationStoreResult> IssueAsync(
        Guid trainerId,
        Guid clientId,
        string email,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken);

    Task<ConsumeClientInvitationStoreResult> ConsumeAsync(
        string rawToken,
        Guid authenticatedUserId,
        bool transferApproved,
        DateTime refreshExpiresAt,
        DateTime now,
        CancellationToken cancellationToken);
}
