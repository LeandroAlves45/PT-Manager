using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Domain.Entities.Supplements;
using Infrastructure.Data;
using Infrastructure.Persistence.ClientPortal;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Supplements;

/// <summary>
/// Marca e desmarca a toma de hoje. A linha da atribuição é trancada para que dois pedidos
/// simultâneos não tentem inserir a mesma toma.
/// </summary>
internal sealed class SupplementIntakeStore : ISupplementIntakeStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;

    public SupplementIntakeStore(
        PtManagerDbContext dbContext,
        ITrainerTimeZoneProvider timeZoneProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
    }

    public Task<SupplementIntakeStoreResult> MarkAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid assignmentId,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(async () =>
        {
            var locked = await LockOwnAssignmentAsync(
                trainerId, clientUserId, assignmentId, now, cancellationToken);
            if (locked.Assignment is null)
                return SupplementIntakeStoreResult.For(
                    SupplementIntakeStoreResult.Status.AssignmentNotFound);
            if (!locked.Assignment.IsActive)
                return SupplementIntakeStoreResult.For(
                    SupplementIntakeStoreResult.Status.AssignmentInactive);

            var existing = await _dbContext.ClientSupplementIntakes
                .AsNoTracking()
                .SingleOrDefaultAsync(intake =>
                    intake.ClientSupplementAssignmentId == assignmentId &&
                    intake.LocalDate == locked.LocalToday,
                    cancellationToken);
            if (existing is not null)
                return SupplementIntakeStoreResult.ForAlreadyMarked(existing);

            var created = new ClientSupplementIntake(
                trainerId,
                locked.Assignment.ClientId,
                assignmentId,
                locked.LocalToday,
                PostgresTimestamps.Truncate(now));
            _dbContext.ClientSupplementIntakes.Add(created);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return SupplementIntakeStoreResult.ForMarked(created);
        }, cancellationToken);

    public Task<SupplementIntakeStoreResult> UnmarkAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid assignmentId,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(async () =>
        {
            // Desmarcar é permitido mesmo numa atribuição entretanto desativada.
            var locked = await LockOwnAssignmentAsync(
                trainerId, clientUserId, assignmentId, now, cancellationToken);
            if (locked.Assignment is null)
                return SupplementIntakeStoreResult.For(
                    SupplementIntakeStoreResult.Status.AssignmentNotFound);

            var existing = await _dbContext.ClientSupplementIntakes
                .SingleOrDefaultAsync(intake =>
                    intake.ClientSupplementAssignmentId == assignmentId &&
                    intake.LocalDate == locked.LocalToday,
                    cancellationToken);
            if (existing is null)
                return SupplementIntakeStoreResult.For(
                    SupplementIntakeStoreResult.Status.NotMarked);

            _dbContext.ClientSupplementIntakes.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return SupplementIntakeStoreResult.For(SupplementIntakeStoreResult.Status.Unmarked);
        }, cancellationToken);

    private async Task<(ClientSupplementAssignment? Assignment, DateOnly LocalToday)> LockOwnAssignmentAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid assignmentId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var timeZone = await _timeZoneProvider.GetRequiredAsync(trainerId, cancellationToken);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, timeZone));

        var clientId = await PortalClients.FindActiveIdAsync(
            _dbContext, trainerId, clientUserId, cancellationToken);
        if (!clientId.HasValue)
            return (null, localToday);

        // O filtro por cliente no SQL impede de tomar a atribuição de outro cliente
        // do mesmo personal trainer.
        var assignment = await _dbContext.ClientSupplementAssignments
            .FromSqlInterpolated($$"""
                SELECT * FROM client_supplement_assignments
                WHERE id = {{assignmentId}}
                    AND owner_trainer_id = {{trainerId}}
                    AND client_id = {{clientId.Value}}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        return (assignment, localToday);
    }

    private async Task<SupplementIntakeStoreResult> ExecuteTransactionAsync(
        Func<Task<SupplementIntakeStoreResult>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                var result = await operation();
                if (result.IsSuccess)
                    await transaction.CommitAsync(cancellationToken);
                else
                    await transaction.RollbackAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }
}
