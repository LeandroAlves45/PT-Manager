using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Domain.Entities.Billing;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Packs;

/// <summary>Persiste packs atribuídos com referências tenant-safe.</summary>
public sealed class ClientSessionPackStore : IClientSessionPackStore
{
    private readonly PtManagerDbContext _dbContext;

    public ClientSessionPackStore(PtManagerDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<ClientSessionPackStoreResult> AssignAsync(
        Guid trainerId,
        Guid clientId,
        Guid packTypeId,
        DateOnly purchaseDate,
        DateOnly? expectedEndDate,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => AssignOnceAsync(
            trainerId,
            clientId,
            packTypeId,
            purchaseDate,
            expectedEndDate,
            now,
            cancellationToken
        ));
    }

    public Task<ClientSessionPackStoreResult> UpdateExpectedEndDateAsync(
        Guid trainerId,
        Guid packId,
        DateOnly? expectedEndDate,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => UpdateExpectedEndDateOnceAsync(
            trainerId,
            packId,
            expectedEndDate,
            now,
            cancellationToken
        ));
    }

    public Task<ClientSessionPackStoreResult> CancelAsync(
        Guid trainerId,
        Guid packId,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => CancelOnceAsync(
            trainerId,
            packId,
            now,
            cancellationToken
        ));
    }

    private async Task<ClientSessionPackStoreResult> AssignOnceAsync(
        Guid trainerId,
        Guid clientId,
        Guid packTypeId,
        DateOnly purchaseDate,
        DateOnly? expectedEndDate,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await _dbContext.Database.
            BeginTransactionAsync(cancellationToken);

        try
        {
            // O lock lineariza a atribuição com ArchiveClient.
            var client = await _dbContext.Clients
                .FromSqlInterpolated($$"""
                    SELECT *
                    FROM clients
                    WHERE owner_trainer_id = {{trainerId}}
                        AND id = {{clientId}}
                        AND is_deleted = false
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);

            if (client is null)
                return await RollbackAsync(
                    transaction,
                    ClientSessionPackStoreResult.Status.ClientNotFound
                );
            if (!client.IsActive)
                return await RollbackAsync(
                    transaction,
                    ClientSessionPackStoreResult.Status.ClientInactive
                );

            // ArchivePackType concorrente espera até o snapshot terminar.
            var packType = await _dbContext.PackTypes
                .FromSqlInterpolated($$"""
                    SELECT *
                    FROM pack_types
                    WHERE owner_trainer_id = {{trainerId}}
                        AND id = {{packTypeId}}
                        AND is_deleted = false
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);

            if (packType is null)
                return await RollbackAsync(
                    transaction,
                    ClientSessionPackStoreResult.Status.PackTypeNotFound
                );
            if (!packType.IsActive)
                return await RollbackAsync(
                    transaction,
                    ClientSessionPackStoreResult.Status.PackTypeInactive
                );

            var pack = new ClientSessionPack(
                trainerId,
                clientId,
                packType,
                purchaseDate,
                expectedEndDate,
                now
            );

            _dbContext.ClientSessionPacks.Add(pack);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ClientSessionPackStoreResult.ForAssigned(pack);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<ClientSessionPackStoreResult> CancelOnceAsync(
        Guid trainerId,
        Guid packId,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await _dbContext.Database.
            BeginTransactionAsync(cancellationToken);

        try
        {
            var pack = await _dbContext.ClientSessionPacks
                .FromSqlInterpolated($$"""
                    SELECT *
                    FROM client_session_packs
                    WHERE owner_trainer_id = {{trainerId}}
                        AND id = {{packId}}
                    FOR UPDATE
                    """)
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(cancellationToken);

            if (pack is null)
                return await RollbackAsync(
                    transaction,
                    ClientSessionPackStoreResult.Status.PackNotFound
                );
            if (pack.IsDeleted)
                return await RollbackAsync(
                    transaction,
                    ClientSessionPackStoreResult.Status.AlreadyInRequestedState
                );
            if (pack.SessionsRemaining != pack.SessionsTotal)
                return await RollbackAsync(
                    transaction,
                    ClientSessionPackStoreResult.Status.PackUsed
                );

            var referenced = await _dbContext.Sessions
                .IgnoreQueryFilters()
                .AnyAsync(
                    session => session.OwnerTrainerId == trainerId
                        && session.ClientSessionPackId == packId,
                    cancellationToken
                );
            if (referenced)
                return await RollbackAsync(
                    transaction,
                    ClientSessionPackStoreResult.Status.PackReferenced
                );
            pack.Cancel(now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ClientSessionPackStoreResult.For(
                ClientSessionPackStoreResult.Status.Cancelled
            );
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<ClientSessionPackStoreResult> UpdateExpectedEndDateOnceAsync(
        Guid trainerId,
        Guid packId,
        DateOnly? expectedEndDate,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await _dbContext.Database.
            BeginTransactionAsync(cancellationToken);

        try
        {
            // Serializa a correção com Cancel e consumo/restore.
            var pack = await _dbContext.ClientSessionPacks
                .FromSqlInterpolated($$"""
                    SELECT *
                    FROM client_session_packs
                    WHERE owner_trainer_id = {{trainerId}}
                        AND id = {{packId}}
                        AND is_deleted = false
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);

            if (pack is null)
                return await RollbackAsync(
                    transaction,
                    ClientSessionPackStoreResult.Status.PackNotFound
                );
            if (expectedEndDate.HasValue && expectedEndDate.Value < pack.PurchaseDate)
                return await RollbackAsync(
                    transaction,
                    ClientSessionPackStoreResult.Status.ExpectedEndDateBeforePurchase
                );
            if (pack.ExpectedEndDate == expectedEndDate)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return ClientSessionPackStoreResult.ForAlreadyInRequested(pack);
            }

            pack.ChangeExpectedEndDate(expectedEndDate, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ClientSessionPackStoreResult.ForUpdated(pack);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<ClientSessionPackStoreResult> RollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        ClientSessionPackStoreResult.Status status
    )
    {
        await transaction.RollbackAsync(CancellationToken.None);
        return ClientSessionPackStoreResult.For(status);
    }
}
