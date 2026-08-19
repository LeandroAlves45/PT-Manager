using Application.Features.Training.Exercises.Abstractions;
using Domain.Entities.Training;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Training;

/// <summary>Persiste exercícios privados sob filtros multi-tenant.</summary>
internal sealed class ExerciseStore : IExerciseStore
{
    private readonly PtManagerDbContext _dbContext;

    public ExerciseStore(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(
        Exercise exercise,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        _dbContext.Exercises.Add(exercise);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExerciseStoreResult> UpdateAsync(
        Guid exerciseId,
        Guid trainerId,
        string name,
        string? description,
        string? muscleGroups,
        string? equipment,
        string? difficultyLevel,
        string? videoUrl,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var exercise = await _dbContext.Exercises.SingleOrDefaultAsync(
            candidate => candidate.Id == exerciseId,
            cancellationToken);

        if (exercise is null)
            return ExerciseStoreResult.ForNotFound();
        if (exercise.OwnerTrainerId is null)
            return ExerciseStoreResult.ForGlobalReadOnly();
        if (exercise.OwnerTrainerId != trainerId)
            return ExerciseStoreResult.ForNotFound();

        exercise.Update(
            name,
            description,
            muscleGroups,
            equipment,
            difficultyLevel,
            videoUrl,
            now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ExerciseStoreResult.ForUpdated(exercise);
    }

    public async Task<ExerciseStoreResult> SetActiveAsync(
        Guid exerciseId,
        Guid trainerId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var affected = await _dbContext.Exercises
            .Where(exercise => exercise.Id == exerciseId &&
                exercise.OwnerTrainerId == trainerId)
            .Where(exercise => exercise.IsActive != isActive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(exercise => exercise.IsActive, isActive)
                    .SetProperty(exercise => exercise.UpdatedAt, now),
                cancellationToken);

        if (affected == 1)
            return ExerciseStoreResult.ForChanged();

        if (affected > 1)
            throw new InvalidOperationException("An Exercise ID must identify one row.");

        var state = await _dbContext.Exercises
            .AsNoTracking()
            .Where(exercise => exercise.Id == exerciseId)
            .Select(exercise => new { exercise.OwnerTrainerId, exercise.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (state is null)
            return ExerciseStoreResult.ForNotFound();
        if (state.OwnerTrainerId is null)
            return ExerciseStoreResult.ForGlobalReadOnly();

        return state.OwnerTrainerId == trainerId && state.IsActive == isActive
            ? ExerciseStoreResult.ForAlreadyRequested()
            : ExerciseStoreResult.ForNotFound();
    }
}
