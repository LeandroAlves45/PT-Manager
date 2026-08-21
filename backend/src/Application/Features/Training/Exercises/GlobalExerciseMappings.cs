using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Results;
using Domain.Entities.Training;

namespace Application.Features.Training.Exercises;

/// <summary>Converte exercícios globais em contratos da Application.</summary>
public static class GlobalExerciseMappings
{
    public static GlobalExerciseDto ToGlobalDto(this Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return new GlobalExerciseDto(
            exercise.Id,
            exercise.Name,
            exercise.Description,
            exercise.MuscleGroups,
            exercise.Equipment,
            exercise.DifficultyLevel,
            exercise.VideoUrl,
            exercise.IsActive,
            exercise.CreatedAt,
            exercise.UpdatedAt
        );
    }

    internal static Result<GlobalExerciseDto> ToDtoResult(this GlobalExerciseStoreResult outcome) =>
        outcome.Kind switch
        {
            GlobalExerciseStoreResult.Status.Created or
            GlobalExerciseStoreResult.Status.Updated =>
                Result<GlobalExerciseDto>.Success(outcome.Exercise!.ToGlobalDto()),
            GlobalExerciseStoreResult.Status.NotFound =>
                Result<GlobalExerciseDto>.Failure(TrainingErrors.ExerciseNotFound),
            GlobalExerciseStoreResult.Status.Inactive =>
                Result<GlobalExerciseDto>.Failure(TrainingErrors.ExerciseInactive),
            GlobalExerciseStoreResult.Status.Referenced =>
                Result<GlobalExerciseDto>.Failure(TrainingErrors.GlobalExerciseReferenced),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    internal static Result ToTransitionResult(this GlobalExerciseStoreResult outcome) =>
        outcome.Kind switch
        {
            GlobalExerciseStoreResult.Status.Changed or
            GlobalExerciseStoreResult.Status.Deleted or
            GlobalExerciseStoreResult.Status.AlreadyInRequestedState => Result.Success(),
            GlobalExerciseStoreResult.Status.NotFound =>
                Result.Failure(TrainingErrors.ExerciseNotFound),
            GlobalExerciseStoreResult.Status.HasReferences =>
                Result.Failure(TrainingErrors.GlobalExerciseHasReferences),
            GlobalExerciseStoreResult.Status.Inactive =>
                Result.Failure(TrainingErrors.ExerciseInactive),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
