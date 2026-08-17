using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Results;

namespace Application.Features.Training.Exercises;

/// <summary>Converte resultados de persistência de exercícios em resultados da Application.</summary>
internal static class ExerciseStoreResultMapper
{
    internal static Result ToTransitionResult(this ExerciseStoreResult outcome) =>
        outcome.Kind switch
        {
            ExerciseStoreResult.Status.Changed or
            ExerciseStoreResult.Status.AlreadyInRequestedState => Result.Success(),
            ExerciseStoreResult.Status.NotFound =>
                Result.Failure(TrainingErrors.ExerciseNotFound),
            ExerciseStoreResult.Status.GlobalReadOnly =>
                Result.Failure(TrainingErrors.GlobalExerciseReadOnly),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    internal static Result<ExerciseDto> ToUpdateResult(this ExerciseStoreResult outcome) =>
        outcome.Kind switch
        {
            ExerciseStoreResult.Status.Updated =>
                Result<ExerciseDto>.Success(outcome.Exercise!.ToDto()),
            ExerciseStoreResult.Status.NotFound =>
                Result<ExerciseDto>.Failure(TrainingErrors.ExerciseNotFound),
            ExerciseStoreResult.Status.GlobalReadOnly =>
                Result<ExerciseDto>.Failure(TrainingErrors.GlobalExerciseReadOnly),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
