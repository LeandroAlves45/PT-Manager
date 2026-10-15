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

    /// <summary>
    /// Cria a conta do cliente convidado, associa-a à ficha e abre a sessão, tudo numa
    /// transação. O email da conta é o do convite, já provado pelo link.
    /// </summary>
    Task<RegisterInvitedClientStoreResult> RegisterClientAsync(
        string rawToken,
        string password,
        DateTime refreshExpiresAt,
        DateTime now,
        CancellationToken cancellationToken);
}
