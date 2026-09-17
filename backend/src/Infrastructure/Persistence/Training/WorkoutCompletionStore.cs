using Application.Common.Abstractions;
using Application.Features.Training.WorkoutCompletions.Abstractions;
using Domain.Entities.Training;
using Infrastructure.Data;
using Infrastructure.Persistence.ClientPortal;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Training;

/// <summary>
/// Persiste a conclusão de treino do cliente sob lock do plano. O lock serializa conclusões
/// concorrentes do mesmo dia e impede a remoção do dia entre a validação e o insert.
/// </summary>
internal sealed class WorkoutCompletionStore : IWorkoutCompletionStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;

    public WorkoutCompletionStore(
        PtManagerDbContext dbContext,
        ITrainerTimeZoneProvider timeZoneProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
    }

    public async Task<WorkoutCompletionStoreResult> CompleteAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid trainingPlanDayId,
        string? notes,
        DateTime now,
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
                var result = await CompleteOnceAsync(
                    trainerId, clientUserId, trainingPlanDayId, notes, now, cancellationToken);
                if (result.Kind == WorkoutCompletionStoreResult.Status.Completed)
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

    private async Task<WorkoutCompletionStoreResult> CompleteOnceAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid trainingPlanDayId,
        string? notes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var timeZone = await _timeZoneProvider.GetRequiredAsync(trainerId, cancellationToken);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, timeZone));

        var clientId = await PortalClients.FindActiveIdAsync(
            _dbContext, trainerId, clientUserId, cancellationToken);
        if (!clientId.HasValue)
            return WorkoutCompletionStoreResult.For(WorkoutCompletionStoreResult.Status.NotFound);

        var planId = await (
            from day in _dbContext.TrainingPlanDays.AsNoTracking()
            join plan in _dbContext.TrainingPlans.AsNoTracking()
                on day.TrainingPlanId equals plan.Id
            where day.Id == trainingPlanDayId &&
                plan.OwnerTrainerId == trainerId &&
                plan.ClientId == clientId.Value
            select (Guid?)plan.Id
        ).SingleOrDefaultAsync(cancellationToken);
        if (!planId.HasValue)
            return WorkoutCompletionStoreResult.For(WorkoutCompletionStoreResult.Status.NotFound);

        var lockedPlan = await TrainingPlanLocks.LockAndLoadAsync(
            _dbContext, planId.Value, trainerId, cancellationToken);
        if (lockedPlan is null)
            return WorkoutCompletionStoreResult.For(WorkoutCompletionStoreResult.Status.NotFound);
        if (!lockedPlan.IsActive || lockedPlan.IsArchived)
            return WorkoutCompletionStoreResult.For(
                WorkoutCompletionStoreResult.Status.TrainingPlanInactive);
        if (!TrainingPlanLocks.Contains(lockedPlan, localToday))
            return WorkoutCompletionStoreResult.For(
                WorkoutCompletionStoreResult.Status.DateOutsidePlan);

        var dayStillInPlan = await _dbContext.TrainingPlanDays
            .AsNoTracking()
            .AnyAsync(day => day.Id == trainingPlanDayId &&
                day.TrainingPlanId == lockedPlan.Id,
                cancellationToken);
        if (!dayStillInPlan)
            return WorkoutCompletionStoreResult.For(
                WorkoutCompletionStoreResult.Status.NotFound);

        var existing = await _dbContext.WorkoutCompletions
            .AsNoTracking()
            .SingleOrDefaultAsync(completion =>
                completion.ClientId == clientId.Value &&
                completion.TrainingPlanDayId == trainingPlanDayId &&
                completion.LocalDate == localToday,
                cancellationToken);
        if (existing is not null)
            return WorkoutCompletionStoreResult.ForAlreadyCompleted(existing);

        var created = new WorkoutCompletion(
            trainerId,
            clientId.Value,
            lockedPlan.Id,
            trainingPlanDayId,
            localToday,
            notes,
            PostgresTimestamps.Truncate(now));
        _dbContext.WorkoutCompletions.Add(created);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return WorkoutCompletionStoreResult.ForCompleted(created);
    }
}
