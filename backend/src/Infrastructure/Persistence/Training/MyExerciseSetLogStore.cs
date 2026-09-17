using Application.Common.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Domain.Entities.Training;
using Infrastructure.Data;
using Infrastructure.Persistence.ClientPortal;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Training;

/// <summary>
/// Persiste as séries registadas pelo próprio cliente. O plano é trancado antes de validar
/// a prescrição, tal como no fluxo do personal trainer, para a estrutura não mudar entre o teste e
/// a escrita.
/// </summary>
internal sealed class MyExerciseSetLogStore : IMyExerciseSetLogStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;

    public MyExerciseSetLogStore(
        PtManagerDbContext dbContext,
        ITrainerTimeZoneProvider timeZoneProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
    }

    public Task<MyExerciseSetLogStoreResult> RecordAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid trainingPlanDayExerciseId,
        int setNumber,
        decimal weightKg,
        int repsDone,
        decimal? rpe,
        string? notes,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(async () =>
        {
            var timeZone = await _timeZoneProvider.GetRequiredAsync(trainerId, cancellationToken);
            var localToday = ToLocalDate(now, timeZone);

            var clientId = await PortalClients.FindActiveIdAsync(
                _dbContext, trainerId, clientUserId, cancellationToken);
            if (!clientId.HasValue)
                return MyExerciseSetLogStoreResult.For(MyExerciseSetLogStoreResult.Status.NotFound);

            var planId = await (
                from exercise in _dbContext.TrainingPlanDayExercises.AsNoTracking()
                join day in _dbContext.TrainingPlanDays.AsNoTracking()
                    on exercise.TrainingPlanDayId equals day.Id
                join plan in _dbContext.TrainingPlans.AsNoTracking()
                    on day.TrainingPlanId equals plan.Id
                where exercise.Id == trainingPlanDayExerciseId &&
                    plan.OwnerTrainerId == trainerId &&
                    plan.ClientId == clientId.Value
                select (Guid?)plan.Id
            ).SingleOrDefaultAsync(cancellationToken);
            if (!planId.HasValue)
                return MyExerciseSetLogStoreResult.For(MyExerciseSetLogStoreResult.Status.NotFound);

            var lockedPlan = await TrainingPlanLocks.LockAndLoadAsync(
                _dbContext, planId.Value, trainerId, cancellationToken);
            if (lockedPlan is null)
                return MyExerciseSetLogStoreResult.For(MyExerciseSetLogStoreResult.Status.NotFound);
            if (!lockedPlan.IsActive || lockedPlan.IsArchived)
                return MyExerciseSetLogStoreResult.For(
                    MyExerciseSetLogStoreResult.Status.TrainingPlanInactive);
            if (!TrainingPlanLocks.Contains(lockedPlan, localToday))
                return MyExerciseSetLogStoreResult.For(
                    MyExerciseSetLogStoreResult.Status.DateOutsidePlan);

            // Revalidação já com lock: a estrutura pode ter mudado antes de o obter.
            var exerciseStillInPlan = await (
                from exercise in _dbContext.TrainingPlanDayExercises.AsNoTracking()
                join day in _dbContext.TrainingPlanDays.AsNoTracking()
                    on exercise.TrainingPlanDayId equals day.Id
                where exercise.Id == trainingPlanDayExerciseId &&
                    day.TrainingPlanId == lockedPlan.Id
                select exercise.Id
            ).AnyAsync(cancellationToken);
            if (!exerciseStillInPlan)
                return MyExerciseSetLogStoreResult.For(
                    MyExerciseSetLogStoreResult.Status.NotFound);

            var setExists = await _dbContext.ExerciseSets
                .AsNoTracking()
                .AnyAsync(set => set.TrainingPlanDayExerciseId == trainingPlanDayExerciseId &&
                    set.SetNumber == setNumber,
                    cancellationToken);
            if (!setExists)
                return MyExerciseSetLogStoreResult.For(
                    MyExerciseSetLogStoreResult.Status.SetNotFound);

            var log = new ClientExerciseSetLog(
                clientId.Value,
                trainingPlanDayExerciseId,
                setNumber,
                weightKg,
                repsDone,
                notes,
                new DateTimeOffset(PostgresTimestamps.Truncate(now)),
                now,
                rpe);
            _dbContext.ClientExerciseSetLogs.Add(log);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return MyExerciseSetLogStoreResult.ForRecorded(log);
        }, cancellationToken);

    public Task<MyExerciseSetLogStoreResult> CorrectAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid exerciseSetLogId,
        decimal weightKg,
        int repsDone,
        decimal? rpe,
        string? notes,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(async () =>
        {
            var owned = await LockOwnLogAsync(
                trainerId, clientUserId, exerciseSetLogId, now, cancellationToken);
            if (owned.Failure is not null)
                return owned.Failure;

            var log = owned.Log!;
            log.Correct(
                weightKg,
                repsDone,
                rpe,
                notes,
                log.PerformedAt,
                now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return MyExerciseSetLogStoreResult.ForCorrected(log);
        }, cancellationToken);

    public Task<MyExerciseSetLogStoreResult> DeleteAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid exerciseSetLogId,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteTransactionAsync(async () =>
        {
            var owned = await LockOwnLogAsync(
                trainerId, clientUserId, exerciseSetLogId, now, cancellationToken);
            if (owned.Failure is not null)
                return owned.Failure;

            // Depois de concluir o treino do dia o histórico fica fechado para o cliente.
            var completed = await _dbContext.WorkoutCompletions
                .AsNoTracking()
                .AnyAsync(completion =>
                    completion.ClientId == owned.Log!.ClientId &&
                    completion.TrainingPlanDayId == owned.TrainingPlanDayId &&
                    completion.LocalDate == owned.LocalToday,
                    cancellationToken);
            if (completed)
                return MyExerciseSetLogStoreResult.For(
                    MyExerciseSetLogStoreResult.Status.WorkoutAlreadyCompleted);

            _dbContext.ClientExerciseSetLogs.Remove(owned.Log!);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return MyExerciseSetLogStoreResult.For(MyExerciseSetLogStoreResult.Status.Deleted);
        }, cancellationToken);

    /// <summary>
    /// Resolve um log do próprio cliente, tranca o plano e confirma que o log é de hoje e
    /// que o plano continua ativo. Devolve o log com tracking para a mutação seguinte.
    /// </summary>
    private async Task<OwnedLog> LockOwnLogAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid exerciseSetLogId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var timeZone = await _timeZoneProvider.GetRequiredAsync(trainerId, cancellationToken);
        var localToday = ToLocalDate(now, timeZone);

        var clientId = await PortalClients.FindActiveIdAsync(
            _dbContext, trainerId, clientUserId, cancellationToken);
        if (!clientId.HasValue)
            return OwnedLog.Fail(MyExerciseSetLogStoreResult.Status.NotFound);

        var reference = await (
            from log in _dbContext.ClientExerciseSetLogs.AsNoTracking()
            join exercise in _dbContext.TrainingPlanDayExercises.AsNoTracking()
                on log.TrainingPlanDayExerciseId equals exercise.Id
            join day in _dbContext.TrainingPlanDays.AsNoTracking()
                on exercise.TrainingPlanDayId equals day.Id
            join plan in _dbContext.TrainingPlans.AsNoTracking()
                on day.TrainingPlanId equals plan.Id
            where log.Id == exerciseSetLogId &&
                log.ClientId == clientId.Value &&
                plan.ClientId == clientId.Value &&
                plan.OwnerTrainerId == trainerId
            select new { PlanId = plan.Id, DayId = day.Id }
        ).SingleOrDefaultAsync(cancellationToken);
        if (reference is null)
            return OwnedLog.Fail(MyExerciseSetLogStoreResult.Status.NotFound);

        var lockedPlan = await TrainingPlanLocks.LockAndLoadAsync(
            _dbContext, reference.PlanId, trainerId, cancellationToken);
        if (lockedPlan is null)
            return OwnedLog.Fail(MyExerciseSetLogStoreResult.Status.NotFound);
        if (!lockedPlan.IsActive || lockedPlan.IsArchived)
            return OwnedLog.Fail(MyExerciseSetLogStoreResult.Status.TrainingPlanInactive);

        var tracked = await _dbContext.ClientExerciseSetLogs
            .SingleOrDefaultAsync(log => log.Id == exerciseSetLogId &&
                log.ClientId == clientId.Value,
                cancellationToken);
        if (tracked is null)
            return OwnedLog.Fail(MyExerciseSetLogStoreResult.Status.NotFound);

        // Só o dia local de hoje é editável pelo cliente; dias anteriores são do personal trainer.
        if (ToLocalDate(tracked.PerformedAt.UtcDateTime, timeZone) != localToday)
            return OwnedLog.Fail(MyExerciseSetLogStoreResult.Status.NotEditable);

        return new OwnedLog(tracked, reference.DayId, localToday, null);
    }

    private static DateOnly ToLocalDate(DateTime utc, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone));

    private async Task<MyExerciseSetLogStoreResult> ExecuteTransactionAsync(
        Func<Task<MyExerciseSetLogStoreResult>> operation,
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
                // O cancelamento da operação não pode impedir a limpeza transacional.
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }

    private sealed record OwnedLog(
        ClientExerciseSetLog? Log,
        Guid TrainingPlanDayId,
        DateOnly LocalToday,
        MyExerciseSetLogStoreResult? Failure)
    {
        public static OwnedLog Fail(MyExerciseSetLogStoreResult.Status status) =>
            new(null, Guid.Empty, default, MyExerciseSetLogStoreResult.For(status));
    }
}
