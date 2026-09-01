using Application.Features.Training.TrainingPlans.Abstractions;
using Domain.Entities.Training;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence.Training;

/// <summary>Persiste planos de treino como operações compostas de tenant-safe.</summary>
internal sealed class TrainingPlanStore : ITrainingPlanStore
{
    private const string ActivePlanConstraint = "uq_training_plan_active_per_client";
    private readonly PtManagerDbContext _dbContext;
    private readonly TrainingPlanStructureCoordinator _structure;

    public TrainingPlanStore(
        PtManagerDbContext dbContext,
        TrainingPlanStructureCoordinator structure)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _structure = structure ?? throw new ArgumentNullException(nameof(structure));
    }

    public Task<TrainingPlanStoreResult> CreateAsync(
        Guid trainerId,
        CreateTrainingPlanWriteModel model,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        ExecuteTransactionAsync(async () =>
        {
            if (!await ActiveClientExistsAsync(model.ClientId, cancellationToken))
                return TrainingPlanStoreResult.ForClientNotFound();

            var referenceStatus = await ValidateExerciseReferencesAsync(
                trainerId,
                model.Structure.Days.SelectMany(day => day.Exercises)
                    .Select(item => item.ExerciseId)
                    .Distinct()
                    .ToArray(),
                cancellationToken);

            var referenceFailure = MapReferenceFailure(referenceStatus);
            if (referenceFailure is not null)
                return referenceFailure;

            var plan = CreatePlan(trainerId, model, now);
            _structure.AddNewStructure(plan, model.Structure, now);
            _dbContext.TrainingPlans.Add(plan);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsActivePlanConflict(ex))
            {
                return TrainingPlanStoreResult.ForActivePlanConflict();
            }

            return TrainingPlanStoreResult.ForCreated(plan.Id);
        }, cancellationToken);

    public Task<TrainingPlanStoreResult> UpdateMetadataAsync(
        Guid trainerId,
        UpdateTrainingPlanMetadataWriteModel model,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        ExecuteTransactionAsync(async () =>
        {
            var plan = await LockAndLoadPlanAsync(
                model.TrainingPlanId,
                trainerId,
                cancellationToken);

            if (plan is null)
                return TrainingPlanStoreResult.ForNotFound();
            if (!plan.IsActive)
                return TrainingPlanStoreResult.ForInactive();

            var hasLogs = await HasLogsAsync(plan.Id, cancellationToken);
            if (hasLogs && (plan.StartDate != model.StartDate || plan.EndDate != model.EndDate))
                return TrainingPlanStoreResult.ForStructureHasHistory();

            plan.UpdateMetadata(
                model.Name,
                model.Description,
                model.TrainingModality,
                model.Notes,
                model.StartDate,
                model.EndDate,
                now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return TrainingPlanStoreResult.ForUpdated(plan.Id);
        }, cancellationToken);

    public Task<TrainingPlanStoreResult> UpdateStructureAsync(
        Guid trainerId,
        UpdateTrainingPlanStructureWriteModel model,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        ExecuteTransactionAsync(async () =>
        {
            var plan = await LockAndLoadPlanAsync(
                model.TrainingPlanId,
                trainerId,
                cancellationToken);

            if (plan is null)
                return TrainingPlanStoreResult.ForNotFound();
            if (!plan.IsActive)
                return TrainingPlanStoreResult.ForInactive();
            if (!_structure.ReferenceBelongToAggregate(plan, model.Structure))
                return TrainingPlanStoreResult.ForStructureReferenceNotFound();

            var hasLogs = await HasLogsAsync(plan.Id, cancellationToken);
            if (hasLogs && _structure.HasForbiddenHistoricalChanges(plan, model.Structure))
                return TrainingPlanStoreResult.ForStructureHasHistory();

            var references = _structure.GetChangedExerciseIds(plan, model.Structure);
            var referenceFailure = MapReferenceFailure(
                await ValidateExerciseReferencesAsync(
                    trainerId,
                    references,
                    cancellationToken));
            if (referenceFailure is not null)
                return referenceFailure;

            var canReorder = await _structure.PrepareUniqueValuesAsync(
                plan,
                model.Structure,
                now,
                cancellationToken);
            if (!canReorder)
                return TrainingPlanStoreResult.ForStructureReorderRequiresFreeSlot();

            _structure.Reconcile(plan, model.Structure, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return TrainingPlanStoreResult.ForUpdated(plan.Id);
        }, cancellationToken);

    public async Task<TrainingPlanStoreResult> ArchiveAsync(
        Guid trainingPlanId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var affected = await _dbContext.TrainingPlans
            .Where(plan => plan.Id == trainingPlanId && plan.OwnerTrainerId == trainerId)
            .Where(plan => !plan.IsArchived)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(plan => plan.IsActive, false)
                    .SetProperty(plan => plan.IsArchived, true)
                    .SetProperty(plan => plan.UpdatedAt, now),
                cancellationToken);

        if (affected == 1)
            return TrainingPlanStoreResult.ForChanged();
        if (affected > 1)
            throw new InvalidOperationException("A TrainingPlan ID must identify one row.");

        var archived = await _dbContext.TrainingPlans
            .AsNoTracking()
            .Where(plan => plan.Id == trainingPlanId && plan.OwnerTrainerId == trainerId)
            .Select(plan => (bool?)plan.IsArchived)
            .SingleOrDefaultAsync(cancellationToken);

        return archived switch
        {
            true => TrainingPlanStoreResult.ForAlreadyArchived(),
            null => TrainingPlanStoreResult.ForNotFound(),
            _ => throw new InvalidOperationException("TrainingPlan state changed unexpectedly.")
        };
    }

    public Task<TrainingPlanStoreResult> ReplaceAsync(
        Guid trainerId,
        ReplaceTrainingPlanWriteModel model,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        ExecuteTransactionAsync(async () =>
        {
            var current = await LockAndLoadPlanAsync(
                model.TrainingPlanId,
                trainerId,
                cancellationToken);
            if (current is null)
                return TrainingPlanStoreResult.ForNotFound();
            if (!current.IsActive)
                return TrainingPlanStoreResult.ForInactive();
            if (!await ActiveClientExistsAsync(current.ClientId, cancellationToken))
                return TrainingPlanStoreResult.ForClientNotFound();

            var referenceFailure = MapReferenceFailure(
                await ValidateExerciseReferencesAsync(
                    trainerId,
                    model.Structure.Days.SelectMany(day => day.Exercises)
                        .Select(item => item.ExerciseId)
                        .Distinct()
                        .ToArray(),
                    cancellationToken));
            if (referenceFailure is not null)
                return referenceFailure;

            current.Archive(now);

            // A escrita do arquivo ocorre antes do INSERT para satisfazer o
            // futuro índice parcial de um plano ativo por cliente.
            await _dbContext.SaveChangesAsync(cancellationToken);

            var replacement = new TrainingPlan(
                trainerId,
                current.ClientId,
                model.Name,
                model.Description,
                model.TrainingModality,
                model.Notes,
                model.StartDate,
                model.EndDate,
                now);
            _structure.AddNewStructure(replacement, model.Structure, now);
            _dbContext.TrainingPlans.Add(replacement);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsActivePlanConflict(ex))
            {
                return TrainingPlanStoreResult.ForActivePlanConflict();
            }

            return TrainingPlanStoreResult.ForReplaced(replacement.Id);
        }, cancellationToken);

    private async Task<TrainingPlan?> LockAndLoadPlanAsync(
        Guid trainingPlanId,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        // A estrutura e logs bloqueiam a mesma raiz antes de qualquer decisão.
        var lockedId = await _dbContext.Database.SqlQuery<Guid>(
            $"SELECT id AS \"Value\" FROM training_plans WHERE id = {trainingPlanId} AND owner_trainer_id = {trainerId} AND is_deleted = false FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (lockedId == Guid.Empty)
            return null;

        return await _dbContext.TrainingPlans
            .Where(plan => plan.Id == lockedId && plan.OwnerTrainerId == trainerId)
            .Include(plan => plan.Days)
                .ThenInclude(day => day.Exercises)
                    .ThenInclude(exercise => exercise.Sets)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
    }

    private Task<bool> HasLogsAsync(
        Guid trainingPlanId,
        CancellationToken cancellationToken) =>
        _dbContext.ClientExerciseSetLogs
            .AsNoTracking()
            .AnyAsync(log => _dbContext.TrainingPlanDayExercises
                .Any(exercise => exercise.Id == log.TrainingPlanDayExerciseId &&
                    _dbContext.TrainingPlanDays
                        .Any(day => day.Id == exercise.TrainingPlanDayId &&
                            day.TrainingPlanId == trainingPlanId)),
                cancellationToken);

    private Task<bool> ActiveClientExistsAsync(
        Guid clientId,
        CancellationToken cancellationToken) =>
        _dbContext.Clients
            .AsNoTracking()
            .AnyAsync(client => client.Id == clientId && client.IsActive, cancellationToken);

    private async Task<ExerciseReferenceStatus> ValidateExerciseReferencesAsync(
        Guid trainerId,
        IReadOnlyCollection<Guid> exerciseIds,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.LockExercisesForShareAsync(
            trainerId,
            exerciseIds,
            cancellationToken);

        if (!rows.Select(row => row.Id).ToHashSet().SetEquals(exerciseIds))
            return ExerciseReferenceStatus.NotFound;

        return rows.Any(row => !row.IsActive ||
                row.PlatformEnforcementStatus == Domain.ValueObjects.PlatformEnforcementStatus.Blocked)
            ? ExerciseReferenceStatus.Inactive
            : ExerciseReferenceStatus.Valid;
    }

    private async Task<TrainingPlanStoreResult> ExecuteTransactionAsync(
        Func<Task<TrainingPlanStoreResult>> operation,
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
                if (result.Kind is TrainingPlanStoreResult.Status.Created or
                    TrainingPlanStoreResult.Status.Updated or
                    TrainingPlanStoreResult.Status.Replaced)
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

    private static TrainingPlan CreatePlan(
        Guid trainerId,
        CreateTrainingPlanWriteModel model,
        DateTime now) => new(
            trainerId,
            model.ClientId,
            model.Name,
            model.Description,
            model.TrainingModality,
            model.Notes,
            model.StartDate,
            model.EndDate,
            now);

    private static TrainingPlanStoreResult? MapReferenceFailure(
        ExerciseReferenceStatus status) => status switch
        {
            ExerciseReferenceStatus.Valid => null,
            ExerciseReferenceStatus.NotFound =>
                TrainingPlanStoreResult.ForExerciseReferenceNotFound(),
            ExerciseReferenceStatus.Inactive =>
                TrainingPlanStoreResult.ForExerciseReferenceInactive(),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };


    private static bool IsActivePlanConflict(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                postgres.ConstraintName == ActivePlanConstraint)
            {
                return true;
            }
        }
        return false;
    }

    private enum ExerciseReferenceStatus
    {
        Valid,
        NotFound,
        Inactive
    }
}
