using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Clients;

/// <summary>
/// Efeitos de tirar um cliente de atividade, partilhados pelo arquivo feito pelo personal
/// trainer e pela eliminação da conta feita pelo próprio cliente. Corre dentro da transação
/// de quem chama.
/// </summary>
internal static class ClientArchiveEffects
{
    /// <summary>
    /// O que o arquivo faz além de tirar o cliente das listas e do limite: cancela as sessões
    /// futuras (sem gastar saldo), consome o saldo que resta nos packs, anula o convite por
    /// aceitar e termina as sessões de login abertas do cliente. Nada disto volta ao reativar.
    /// O histórico (sessões feitas, planos, avaliações) fica.
    /// </summary>
    public static async Task ApplyAsync(
        PtManagerDbContext dbContext,
        Guid clientId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var nowOffset = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));

        await dbContext.Sessions
            .Where(session =>
                session.OwnerTrainerId == trainerId &&
                session.ClientId == clientId &&
                !session.IsDeleted &&
                session.Status == SessionStatus.Scheduled &&
                session.StartsAt > nowOffset)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.Status, SessionStatus.CancelledByTrainer)
                    .SetProperty(session => session.StatusChangedAt, now)
                    .SetProperty(session => session.UpdatedAt, now),
                cancellationToken);

        await dbContext.ClientSessionPacks
            .Where(pack =>
                pack.OwnerTrainerId == trainerId &&
                pack.ClientId == clientId &&
                !pack.IsDeleted &&
                pack.SessionsRemaining > 0)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(pack => pack.SessionsRemaining, 0)
                    .SetProperty(pack => pack.CompletedAt, now)
                    .SetProperty(pack => pack.ForfeitedAt, now)
                    .SetProperty(pack => pack.UpdatedAt, now),
                cancellationToken);

        await dbContext.InviteTokens
            .Where(token =>
                token.TrainerId == trainerId &&
                token.ClientId == clientId &&
                token.UsedAt == null)
            .ExecuteDeleteAsync(cancellationToken);

        var userId = await dbContext.Clients
            .AsNoTracking()
            .Where(client => client.Id == clientId && client.OwnerTrainerId == trainerId)
            .Select(client => client.UserId)
            .SingleAsync(cancellationToken);
        if (userId.HasValue)
            await dbContext.RefreshTokens
                .Where(token => token.UserId == userId.Value && token.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(token => token.RevokedAt, now),
                    cancellationToken);
    }

    /// <summary>
    /// Liberta o lugar do cliente no limite do plano. Devolve <c>false</c> se o personal
    /// trainer não tiver subscrição (estado que não devia existir).
    /// </summary>
    public static async Task<bool> ReleaseSubscriptionSlotAsync(
        PtManagerDbContext dbContext,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var affected = await dbContext.TrainerSubscriptions
            .Where(subscription => subscription.TrainerId == trainerId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        subscription => subscription.CurrentClientCount,
                        subscription => subscription.CurrentClientCount > 0
                            ? subscription.CurrentClientCount - 1
                            : 0)
                    .SetProperty(subscription => subscription.UpdatedAt, now),
                cancellationToken);

        return affected > 0;
    }
}
