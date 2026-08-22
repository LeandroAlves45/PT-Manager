using System.Data;
using System.Text.Json;
using Application.Features.Training.Exercises.Abstractions;
using Domain.Entities.Administration;
using Domain.Entities.Training;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence.Training;

/// <summary>Persiste mutações globais de exercícios e auditoria na mesma transação.</summary>
internal sealed class GlobalExerciseStore : IGlobalExerciseStore
{
    private const string ResourceType = "exercise";
    private readonly PtManagerDbContext _dbContext;
    private readonly PostgresConstraintTranslator _translator;

    public GlobalExerciseStore(
        PtManagerDbContext dbContext,
        PostgresConstraintTranslator translator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    public Task<GlobalExerciseStoreResult> CreateAsync(
        Guid actorUserId,
        string name,
        string? description,
        string? muscleGroups,
        string? equipment,
        string? difficultyLevel,
        string? videoUrl,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // A mesma identidade tem de sobreviver a uma tentativa repetida após falha transitória.
        var exercise = new Exercise(
            null, name, description, muscleGroups, equipment, difficultyLevel, videoUrl, now);
        var attempt = new MutationAttempt();
        return ExecuteAsync(
            token => CreateOnceAsync(actorUserId, exercise, now, attempt, token),
            attempt,
            cancellationToken);
    }

    public Task<GlobalExerciseStoreResult> UpdateAsync(
        Guid actorUserId,
        Guid exerciseId,
        string name,
        string? description,
        string? muscleGroups,
        string? equipment,
        string? difficultyLevel,
        string? videoUrl,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        return ExecuteAsync(
            token => UpdateOnceAsync(
                actorUserId,
                exerciseId,
                name,
                description,
                muscleGroups,
                equipment,
                difficultyLevel,
                videoUrl,
                now,
                attempt,
                token),
            attempt,
            cancellationToken);
    }

    public Task<GlobalExerciseStoreResult> SetActiveAsync(
        Guid actorUserId,
        Guid exerciseId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        return ExecuteAsync(
            token => SetActiveOnceAsync(
                actorUserId, exerciseId, isActive, now, attempt, token),
            attempt,
            cancellationToken);
    }

    public async Task<GlobalExerciseStoreResult> DeleteAsync(
        Guid actorUserId,
        Guid exerciseId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var attempt = new MutationAttempt();
        try
        {
            return await ExecuteAsync(
                token => DeleteOnceAsync(actorUserId, exerciseId, now, attempt, token),
                attempt,
                cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (_translator.TryTranslate(
                ex,
                PersistenceOperation.DeleteGlobalExercise,
                out var error) && error?.Code == "global_exercise_has_references")
                return GlobalExerciseStoreResult.For(
                    GlobalExerciseStoreResult.Status.HasReferences);
            throw;
        }
    }

    private async Task<GlobalExerciseStoreResult> CreateOnceAsync(
        Guid actorUserId,
        Exercise exercise,
        DateTime now,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        _dbContext.Exercises.Add(exercise);
        attempt.AuditEntry = AddAudit(
            actorUserId, "create", exercise, null, Snapshot(exercise), now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return GlobalExerciseStoreResult.WithExercise(
            GlobalExerciseStoreResult.Status.Created,
            exercise);
    }

    private async Task<GlobalExerciseStoreResult> UpdateOnceAsync(
        Guid actorUserId,
        Guid exerciseId,
        string name,
        string? description,
        string? muscleGroups,
        string? equipment,
        string? difficultyLevel,
        string? videoUrl,
        DateTime now,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        var exercise = await _dbContext.LockGlobalExerciseAsync(exerciseId, cancellationToken);
        if (exercise is null)
            return GlobalExerciseStoreResult.For(GlobalExerciseStoreResult.Status.NotFound);
        if (!exercise.IsActive)
            return GlobalExerciseStoreResult.For(GlobalExerciseStoreResult.Status.Inactive);

        if (await _dbContext.TrainingPlanDayExercises
            .IgnoreQueryFilters()
            .AnyAsync(item => item.ExerciseId == exerciseId, cancellationToken))
            return GlobalExerciseStoreResult.For(GlobalExerciseStoreResult.Status.Referenced);

        var before = Snapshot(exercise);
        exercise.Update(
            name,
            description,
            muscleGroups,
            equipment,
            difficultyLevel,
            videoUrl,
            now);
        attempt.AuditEntry = AddAudit(
            actorUserId, "update", exercise, before, Snapshot(exercise), now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return GlobalExerciseStoreResult.WithExercise(
            GlobalExerciseStoreResult.Status.Updated,
            exercise);
    }

    private async Task<GlobalExerciseStoreResult> SetActiveOnceAsync(
        Guid actorUserId,
        Guid exerciseId,
        bool isActive,
        DateTime now,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        var exercise = await _dbContext.LockGlobalExerciseAsync(exerciseId, cancellationToken);
        if (exercise is null)
            return GlobalExerciseStoreResult.For(GlobalExerciseStoreResult.Status.NotFound);

        if (exercise.IsActive == isActive)
            return GlobalExerciseStoreResult.For(
                GlobalExerciseStoreResult.Status.AlreadyInRequestedState);

        var before = Snapshot(exercise);
        exercise.SetActive(isActive, now);
        attempt.AuditEntry = AddAudit(
            actorUserId,
            isActive ? "reactivate" : "archive",
            exercise,
            before,
            Snapshot(exercise),
            now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return GlobalExerciseStoreResult.For(GlobalExerciseStoreResult.Status.Changed);
    }

    private async Task<GlobalExerciseStoreResult> DeleteOnceAsync(
        Guid actorUserId,
        Guid exerciseId,
        DateTime now,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        var exercise = await _dbContext.LockGlobalExerciseAsync(exerciseId, cancellationToken);
        if (exercise is null)
            return GlobalExerciseStoreResult.For(GlobalExerciseStoreResult.Status.NotFound);

        if (await _dbContext.TrainingPlanDayExercises
            .IgnoreQueryFilters()
            .AnyAsync(item => item.ExerciseId == exerciseId, cancellationToken))
            return GlobalExerciseStoreResult.For(GlobalExerciseStoreResult.Status.HasReferences);

        var before = Snapshot(exercise);
        _dbContext.Exercises.Remove(exercise);
        attempt.AuditEntry = AddAudit(actorUserId, "delete", exercise, before, null, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return GlobalExerciseStoreResult.For(GlobalExerciseStoreResult.Status.Deleted);
    }

    private AdministrativeAuditEntry AddAudit(
        Guid actorUserId,
        string action,
        Exercise exercise,
        string? before,
        string? after,
        DateTime now)
    {
        var entry = new AdministrativeAuditEntry(
            actorUserId, action, ResourceType, exercise.Id, before, after, now);
        _dbContext.AdministrativeAuditEntries.Add(entry);
        return entry;
    }

    private static string Snapshot(Exercise exercise) => JsonSerializer.Serialize(new
    {
        id = exercise.Id,
        name = exercise.Name,
        description = exercise.Description,
        muscle_groups = exercise.MuscleGroups,
        equipment = exercise.Equipment,
        difficulty_level = exercise.DifficultyLevel,
        video_url = exercise.VideoUrl,
        is_active = exercise.IsActive,
        created_at = exercise.CreatedAt,
        updated_at = exercise.UpdatedAt
    });

    private Task<GlobalExerciseStoreResult> ExecuteAsync(
        Func<CancellationToken, Task<GlobalExerciseStoreResult>> operation,
        MutationAttempt attempt,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteInTransactionAsync(
            async operationToken =>
            {
                // Uma tentativa repetida reconstrói tracking e a prova do commit.
                _dbContext.ChangeTracker.Clear();
                attempt.AuditEntry = null;
                return await operation(operationToken);
            },
            verificationToken => VerifySucceededAsync(attempt, verificationToken),
            IsolationLevel.ReadCommitted,
            cancellationToken);
    }

    private Task<bool> VerifySucceededAsync(
        MutationAttempt attempt,
        CancellationToken cancellationToken) =>
        attempt.AuditEntry is null
            ? Task.FromResult(false)
            : _dbContext.AdministrativeAuditEntries
                .AsNoTracking()
                .AnyAsync(entry => entry.Id == attempt.AuditEntry.Id, cancellationToken);

    private sealed class MutationAttempt
    {
        public AdministrativeAuditEntry? AuditEntry { get; set; }
    }
}
