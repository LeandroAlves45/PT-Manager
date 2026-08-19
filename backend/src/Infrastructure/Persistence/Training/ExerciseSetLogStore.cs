using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Domain.Entities.Training;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Training;

/// <summary>
/// Persiste eventos de treino e protege a consistência entre histórico e estrutura.
/// </summary>
internal sealed class ExerciseSetLogStore : IExerciseSetLogStore
{
    private readonly PtManagerDbContext _dbContext;

    public ExerciseSetLogStore(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<ExerciseSetLogStoreResult> RecordAsync(
        Guid trainerId,
        RecordExerciseSetLogWriteModel model,
        DateTimeOffset currentInstant,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(async () =>
        {
            var planId = await ResolvePlanIdForExerciseAsync(
                model.TrainingPlanDayExerciseId,
                trainerId,
                cancellationToken);
            if (!planId.HasValue)
                return ExerciseSetLogStoreResult.ForNotFound();

            var plan = await LockAndLoadPlanAsync(planId.Value, trainerId, cancellationToken);
            if (plan is null)
                return ExerciseSetLogStoreResult.ForNotFound();
            if (!plan.IsActive || plan.IsArchived)
                return ExerciseSetLogStoreResult.ForTrainingPlanInactive();

            // O lock da raiz foi adquirido antes desta validação. UpdateStructure
            // não pode remover a série entre o teste e a inserção do log.
            var exerciseExists = await (
                from exercise in _dbContext.TrainingPlanDayExercises.AsNoTracking()
                join day in _dbContext.TrainingPlanDays.AsNoTracking()
                    on exercise.TrainingPlanDayId equals day.Id
                where exercise.Id == model.TrainingPlanDayExerciseId &&
                    day.TrainingPlanId == plan.Id
                select exercise.Id
            ).AnyAsync(cancellationToken);
            if (!exerciseExists)
                return ExerciseSetLogStoreResult.ForNotFound();

            var setExists = await _dbContext.ExerciseSets
                .AsNoTracking()
                .AnyAsync(set => set.TrainingPlanDayExerciseId == model.TrainingPlanDayExerciseId
                    && set.SetNumber == model.SetNumber,
                    cancellationToken);
            if (!setExists)
                return ExerciseSetLogStoreResult.ForSetNotFound();

            var performedAt = model.PerformedAt.ToUniversalTime();
            var temporalFailure = ValidatePerformedAt(plan, performedAt, currentInstant);
            if (temporalFailure is not null)
                return temporalFailure;

            var log = new ClientExerciseSetLog(
                plan.ClientId,
                model.TrainingPlanDayExerciseId,
                model.SetNumber,
                model.WeightKg,
                model.RepsDone,
                model.Notes,
                performedAt,
                now);
            _dbContext.ClientExerciseSetLogs.Add(log);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ExerciseSetLogStoreResult.ForRecorded(log);
        }, cancellationToken);

    public Task<ExerciseSetLogStoreResult> CorrectAsync(
        Guid trainerId,
        CorrectExerciseSetLogWriteModel model,
        DateTimeOffset currentInstant,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(async () =>
        {
            var planId = await ResolvePlanIdForLogAsync(
                model.ExerciseSetLogId,
                trainerId,
                cancellationToken);
            if (!planId.HasValue)
                return ExerciseSetLogStoreResult.ForNotFound();

            var plan = await LockAndLoadPlanAsync(planId.Value, trainerId, cancellationToken);
            if (plan is null)
                return ExerciseSetLogStoreResult.ForNotFound();

            // Correct aceita planos arquivados porque corrige um evento histórico
            // e não cria uma nova execução nem reativa o plano.
            var log = await _dbContext.ClientExerciseSetLogs
                .SingleOrDefaultAsync(candidate => candidate.Id == model.ExerciseSetLogId, cancellationToken);
            if (log is null)
                return ExerciseSetLogStoreResult.ForNotFound();

            var setStillExists = await _dbContext.ExerciseSets
                .AsNoTracking()
                .AnyAsync(set => set.TrainingPlanDayExerciseId == log.TrainingPlanDayExerciseId
                    && set.SetNumber == log.SetNumber,
                    cancellationToken);
            if (!setStillExists)
                return ExerciseSetLogStoreResult.ForSetNotFound();

            var performedAt = model.PerformedAt.ToUniversalTime();
            var temporalFailure = ValidatePerformedAt(plan, performedAt, currentInstant);
            if (temporalFailure is not null)
                return temporalFailure;

            log.Correct(
                model.WeightKg,
                model.RepsDone,
                model.Notes,
                performedAt,
                now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ExerciseSetLogStoreResult.ForCorrected(log);
        }, cancellationToken);

    private async Task<Guid?> ResolvePlanIdForExerciseAsync(
        Guid dayExerciseId,
        Guid trainerId,
        CancellationToken cancellationToken) =>
        await (
            from exercise in _dbContext.TrainingPlanDayExercises.AsNoTracking()
            join day in _dbContext.TrainingPlanDays.AsNoTracking()
                on exercise.TrainingPlanDayId equals day.Id
            join plan in _dbContext.TrainingPlans.AsNoTracking()
                on day.TrainingPlanId equals plan.Id
            where exercise.Id == dayExerciseId && plan.OwnerTrainerId == trainerId
            select (Guid?)plan.Id
        ).SingleOrDefaultAsync(cancellationToken);

    private async Task<Guid?> ResolvePlanIdForLogAsync(
        Guid logId,
        Guid trainerId,
        CancellationToken cancellationToken) =>
        await (
            from log in _dbContext.ClientExerciseSetLogs.AsNoTracking()
            join exercise in _dbContext.TrainingPlanDayExercises.AsNoTracking()
                on log.TrainingPlanDayExerciseId equals exercise.Id
            join day in _dbContext.TrainingPlanDays.AsNoTracking()
                on exercise.TrainingPlanDayId equals day.Id
            join plan in _dbContext.TrainingPlans.AsNoTracking()
                on day.TrainingPlanId equals plan.Id
            where log.Id == logId && plan.OwnerTrainerId == trainerId
            select (Guid?)plan.Id
        ).SingleOrDefaultAsync(cancellationToken);

    private async Task<TrainingPlan?> LockAndLoadPlanAsync(
        Guid planId,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        // TrainingPlanStore usa exatamente esta linha como mutex relacional.
        var lockedId = await _dbContext.Database.SqlQuery<Guid>(
            $"SELECT id AS \"Value\" FROM training_plans WHERE id = {planId} AND owner_trainer_id = {trainerId} AND is_deleted = false FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (lockedId == Guid.Empty)
            return null;

        return await _dbContext.TrainingPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(plan =>
                plan.Id == lockedId && plan.OwnerTrainerId == trainerId,
                cancellationToken);
    }

    private static ExerciseSetLogStoreResult? ValidatePerformedAt(
        TrainingPlan plan,
        DateTimeOffset performedAt,
        DateTimeOffset currentInstant)
    {
        if (performedAt > currentInstant.ToUniversalTime())
            return ExerciseSetLogStoreResult.ForPerformedAtInFuture();

        var performedDate = DateOnly.FromDateTime(performedAt.UtcDateTime);
        if (performedDate < plan.StartDate ||
            plan.EndDate.HasValue && performedDate > plan.EndDate.Value)
            return ExerciseSetLogStoreResult.ForPerformedAtOutsidePlan();

        return null;
    }

    private async Task<ExerciseSetLogStoreResult> ExecuteTransactionAsync(
        Func<Task<ExerciseSetLogStoreResult>> operation,
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
                if (result.Kind is ExerciseSetLogStoreResult.Status.Recorded or
                    ExerciseSetLogStoreResult.Status.Corrected)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                return result;
            }
            catch
            {
                // O cancelamento da operação não pode impedir a limpeza transacional.
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }
}
