using Application.Features.Supplements.Abstractions;
using Domain.Entities.Supplements;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Supplements;

/// <summary>Persiste atribuições com validações e locks tenant-safe.</summary>
internal sealed class ClientSupplementAssignmentStore : IClientSupplementAssignmentStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly PostgresConstraintTranslator _translator;

    public ClientSupplementAssignmentStore(
        PtManagerDbContext dbContext,
        PostgresConstraintTranslator translator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    public Task<ClientSupplementAssignmentStoreResult> AssignAsync(
        Guid trainerId,
        Guid clientId,
        Guid supplementId,
        string? servingSize,
        string? timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => AssignOnceAsync(
            trainerId, clientId, supplementId, servingSize, timing,
            trainerNotes, now, cancellationToken));
    }

    public Task<ClientSupplementAssignmentStoreResult> UpdateInstructionsAsync(
        Guid trainerId,
        Guid assignmentId,
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => UpdateInstructionsOnceAsync(
            trainerId, assignmentId, servingSize, timing, trainerNotes,
            now, cancellationToken));
    }

    public Task<ClientSupplementAssignmentStoreResult> SetActiveAsync(
        Guid trainerId,
        Guid assignmentId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => SetActiveOnceAsync(
            trainerId, assignmentId, isActive, now, cancellationToken));
    }

    private async Task<ClientSupplementAssignmentStoreResult> AssignOnceAsync(
        Guid trainerId,
        Guid clientId,
        Guid supplementId,
        string? servingSize,
        string? timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var client = await _dbContext.LockClientAsync(trainerId, clientId, cancellationToken);
            if (client is null)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.ClientNotFound
                );
            if (!client.IsActive)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.ClientInactive
                );

            var supplement = await _dbContext.LockVisibleSupplementAsync(
                trainerId, supplementId, cancellationToken);
            if (supplement is null)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.SupplementNotFound
                );
            if (!supplement.IsActive)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.SupplementInactive
                );

            var existing = await _dbContext.ClientSupplementAssignments
                .FromSqlInterpolated($$"""
                    SELECT * FROM client_supplement_assignments
                    WHERE owner_trainer_id = {{trainerId}}
                        AND client_id = {{clientId}}
                        AND supplement_id = {{supplementId}}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.AssignmentAlreadyExists
                );

            var assignment = new ClientSupplementAssignment(
                trainerId, clientId, supplementId,
                servingSize ?? supplement.ServingSize,
                timing ?? supplement.Timing,
                trainerNotes, now);
            _dbContext.ClientSupplementAssignments.Add(assignment);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ClientSupplementAssignmentStoreResult.WithEntities(
                ClientSupplementAssignmentStoreResult.Status.Assigned,
                assignment, supplement);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            if (_translator.TryTranslate(
                ex,
                PersistenceOperation.AssignSupplement,
                out var error) && error?.Code == "supplement_assignment_already_exists")
                return ClientSupplementAssignmentStoreResult.For(
                    ClientSupplementAssignmentStoreResult.Status.AssignmentAlreadyExists);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<ClientSupplementAssignmentStoreResult> UpdateInstructionsOnceAsync(
        Guid trainerId,
        Guid assignmentId,
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var identity = await GetAssignmentIdentityAsync(
                trainerId, assignmentId, cancellationToken);
            if (identity is null)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.AssignmentNotFound
                );

            var client = await _dbContext.LockClientAsync(
                trainerId, identity.ClientId, cancellationToken);
            if (client is null)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.ClientNotFound
                );
            if (!client.IsActive)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.ClientInactive
                );

            var supplement = await _dbContext.LockVisibleSupplementAsync(
                trainerId, identity.SupplementId, cancellationToken);
            if (supplement is null)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.SupplementNotFound
                );
            if (!supplement.IsActive)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.SupplementInactive
                );

            var assignment = await _dbContext.LockAssignmentAsync(
                trainerId, assignmentId, cancellationToken);
            if (assignment is null)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.AssignmentNotFound
                );

            assignment.UpdateInstructions(servingSize, timing, trainerNotes, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ClientSupplementAssignmentStoreResult.WithEntities(
                ClientSupplementAssignmentStoreResult.Status.Updated,
                assignment, supplement);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<ClientSupplementAssignmentStoreResult> SetActiveOnceAsync(
        Guid trainerId,
        Guid assignmentId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var identity = await GetAssignmentIdentityAsync(
                trainerId, assignmentId, cancellationToken);
            if (identity is null)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.AssignmentNotFound
                );

            var client = await _dbContext.LockClientAsync(
                trainerId, identity.ClientId, cancellationToken);
            if (client is null)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.ClientNotFound
                );

            var supplement = await _dbContext.LockVisibleSupplementAsync(
                trainerId, identity.SupplementId, cancellationToken);
            if (supplement is null)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.SupplementNotFound
                );

            var assignment = await _dbContext.LockAssignmentAsync(
                trainerId, assignmentId, cancellationToken);
            if (assignment is null)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.AssignmentNotFound
                );

            if (assignment.IsActive == isActive)
                return await RollbackWithEntitiesAsync(
                    transaction,
                    assignment,
                    supplement,
                    ClientSupplementAssignmentStoreResult.Status.AlreadyInRequestedState
                );
            if (isActive && !client.IsActive)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.ClientInactive
                );
            if (isActive && !supplement.IsActive)
                return await RollbackAsync(
                    transaction,
                    ClientSupplementAssignmentStoreResult.Status.SupplementInactive
                );

            if (isActive)
                assignment.Reactivate(now);
            else
                assignment.Deactivate(now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ClientSupplementAssignmentStoreResult.WithEntities(
                ClientSupplementAssignmentStoreResult.Status.Changed,
                assignment, supplement);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            if (_translator.TryTranslate(
                ex,
                PersistenceOperation.ReactivateSupplementAssignment,
                out var error) && error?.Code == "supplement_assignment_already_exists")
                return ClientSupplementAssignmentStoreResult.For(
                    ClientSupplementAssignmentStoreResult.Status.AssignmentAlreadyExists);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private Task<AssignmentIdentity?> GetAssignmentIdentityAsync(
        Guid trainerId,
        Guid assignmentId,
        CancellationToken cancellationToken) => _dbContext.ClientSupplementAssignments
        .AsNoTracking()
        .Where(item => item.OwnerTrainerId == trainerId && item.Id == assignmentId)
        .Select(item => new AssignmentIdentity(item.ClientId, item.SupplementId))
        .SingleOrDefaultAsync(cancellationToken);

    private static async Task<ClientSupplementAssignmentStoreResult> RollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        ClientSupplementAssignmentStoreResult.Status status)
    {
        await transaction.RollbackAsync(CancellationToken.None);
        return ClientSupplementAssignmentStoreResult.For(status);
    }

    private static async Task<ClientSupplementAssignmentStoreResult> RollbackWithEntitiesAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        ClientSupplementAssignment assignment,
        Supplement supplement,
        ClientSupplementAssignmentStoreResult.Status status)
    {
        await transaction.RollbackAsync(CancellationToken.None);
        return ClientSupplementAssignmentStoreResult.WithEntities(status, assignment, supplement);
    }

    private sealed record AssignmentIdentity(Guid ClientId, Guid SupplementId);
}
