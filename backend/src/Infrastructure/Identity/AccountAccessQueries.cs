using Application.Common.Abstractions;
using Domain.Entities.Administration;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>
/// Lê numa só consulta indexada se a conta autenticada ainda pode usar a app: a conta ativa,
/// para o cliente a ficha ativa no personal trainer do token, e o bloqueio do personal
/// trainer (com os dias de acesso dos clientes).
/// </summary>
internal sealed class AccountAccessQueries : IAccountAccessQueries
{
    private readonly PtManagerDbContext _dbContext;

    public AccountAccessQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<AccountAccessState> GetAsync(
        Guid userId,
        string role,
        Guid? trainerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters é explícito: a leitura restringe-se às identidades do token.
        var users = _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.Id == userId && user.Role == role && user.IsActive && !user.IsDeleted);

        if (role == "client")
        {
            var client = await users
                .Select(user => new
                {
                    HasActiveRelationship = _dbContext.Clients
                        .IgnoreQueryFilters()
                        .Any(item =>
                            item.UserId == user.Id &&
                            item.OwnerTrainerId == trainerId &&
                            item.IsActive &&
                            !item.IsDeleted),
                    BlockedAt = _dbContext.TrainerAccessBlocks
                        .Where(block => block.TrainerId == trainerId)
                        .Select(block => (DateTime?)block.BlockedAt)
                        .FirstOrDefault()
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (client is null || !client.HasActiveRelationship)
                return AccountAccessState.Ended;
            return client.BlockedAt.HasValue && TrainerAccessBlock.ClientAccessEnded(client.BlockedAt.Value, now)
                ? AccountAccessState.ClientAccessPeriodEnded
                : AccountAccessState.Allowed;
        }

        if (role == "trainer")
        {
            var trainer = await users
                .Select(user => new
                {
                    Block = _dbContext.TrainerAccessBlocks
                        .Where(block => block.TrainerId == user.Id)
                        .Select(block => new { block.Reason, block.Note })
                        .FirstOrDefault()
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (trainer is null)
                return AccountAccessState.Ended;
            return trainer.Block is null
                ? AccountAccessState.Allowed
                : new AccountAccessState(
                    AccountAccessStatus.TrainerBlocked, trainer.Block.Reason.Value, trainer.Block.Note);
        }

        return await users.AnyAsync(cancellationToken) ? AccountAccessState.Allowed : AccountAccessState.Ended;
    }

    public Task<DateTime?> GetTrainerBlockedAtAsync(Guid trainerId, CancellationToken cancellationToken) =>
        _dbContext.TrainerAccessBlocks
            .AsNoTracking()
            .Where(block => block.TrainerId == trainerId)
            .Select(block => (DateTime?)block.BlockedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
