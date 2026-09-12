namespace Application.Features.ClientPortal.Abstractions;

/// <summary>Escreve o avatar do cliente associado á conta autenticada.</summary>
public interface IMyAvatarStore
{
    /// <summary>
    /// Substitui o avatar e, na mesma transação, agenda por outbox a eliminação do asset
    /// anterior quando existia e é diferente do novo.
    /// </summary>
    Task<MyAvatarOutcome> ReplaceAsync(
        Guid trainerId,
        Guid clientUserId,
        string avatarUrl,
        string avatarPublicId,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Remove o avatar e agenda a eliminação do asset. É idempotente.
    /// </summary>
    Task<MyAvatarOutcome> RemoveAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken);
}
