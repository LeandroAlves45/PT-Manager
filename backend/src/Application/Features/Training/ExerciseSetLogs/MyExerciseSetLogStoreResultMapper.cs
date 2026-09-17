using Application.Errors;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Dtos;
using Application.Results;
using Domain.Entities.Training;

namespace Application.Features.Training.ExerciseSetLogs;

/// <summary>Converte resultados das escritas do cliente em contratos da Application.</summary>
internal static class MyExerciseSetLogStoreResultMapper
{
    internal static MyExerciseSetLogDto ToMyDto(this ClientExerciseSetLog log) => new(
        log.Id,
        log.TrainingPlanDayExerciseId,
        log.SetNumber,
        log.WeightKg,
        log.RepsDone,
        log.Rpe,
        log.Notes,
        log.PerformedAt,
        log.UpdatedAt);

    /// <summary>Resultado de registo ou correção com o log persistido.</summary>
    internal static Result<MyExerciseSetLogDto> ToDtoResult(
        this MyExerciseSetLogStoreResult outcome,
        Error notFound) =>
        outcome.IsSuccess && outcome.Log is not null
            ? Result<MyExerciseSetLogDto>.Success(outcome.Log.ToMyDto())
            : Result<MyExerciseSetLogDto>.Failure(outcome.ToError(notFound));

    /// <summary>Resultado sem corpo para a remoção.</summary>
    internal static Result ToResult(this MyExerciseSetLogStoreResult outcome) =>
        outcome.Kind == MyExerciseSetLogStoreResult.Status.Deleted
            ? Result.Success()
            : Result.Failure(outcome.ToError(TrainingErrors.ExerciseSetLogNotFound));

    private static Error ToError(this MyExerciseSetLogStoreResult outcome, Error notFound) =>
        outcome.Kind switch
        {
            MyExerciseSetLogStoreResult.Status.NotFound => notFound,
            MyExerciseSetLogStoreResult.Status.TrainingPlanInactive =>
                TrainingErrors.TrainingPlanInactive,
            MyExerciseSetLogStoreResult.Status.SetNotFound => TrainingErrors.SetNotFound,
            MyExerciseSetLogStoreResult.Status.DateOutsidePlan => TrainingErrors.TrainingDateOutsidePlan,
            MyExerciseSetLogStoreResult.Status.NotEditable => TrainingErrors.ExerciseSetLogNotEditable,
            MyExerciseSetLogStoreResult.Status.WorkoutAlreadyCompleted =>
                TrainingErrors.WorkoutAlreadyCompleted,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
}
