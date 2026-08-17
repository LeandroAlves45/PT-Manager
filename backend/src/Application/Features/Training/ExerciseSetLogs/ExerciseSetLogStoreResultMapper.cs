using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Dtos;
using Application.Results;

namespace Application.Features.Training.ExerciseSetLogs;

/// <summary>Converte falhas de persistência de logs em resultados da Application.</summary>
internal static class ExerciseSetLogStoreResultMapper
{
    internal static Result<ClientExerciseSetLogDto> ToRecordFailure(
        this ExerciseSetLogStoreResult outcome) =>
        outcome.Kind switch
        {
            ExerciseSetLogStoreResult.Status.NotFound =>
                Result<ClientExerciseSetLogDto>.Failure(
                    TrainingErrors.StructureReferenceNotFound),
            ExerciseSetLogStoreResult.Status.TrainingPlanInactive =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.TrainingPlanInactive),
            ExerciseSetLogStoreResult.Status.SetNotFound =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.SetNotFound),
            ExerciseSetLogStoreResult.Status.PerformedAtInFuture =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.PerformedAtInFuture()),
            ExerciseSetLogStoreResult.Status.PerformedAtOutsidePlan =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.PerformedAtOutsidePlan()),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    internal static Result<ClientExerciseSetLogDto> ToCorrectionFailure(
        this ExerciseSetLogStoreResult outcome) =>
        outcome.Kind switch
        {
            ExerciseSetLogStoreResult.Status.NotFound =>
                Result<ClientExerciseSetLogDto>.Failure(
                    TrainingErrors.ExerciseSetLogNotFound),
            ExerciseSetLogStoreResult.Status.SetNotFound =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.SetNotFound),
            ExerciseSetLogStoreResult.Status.PerformedAtInFuture =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.PerformedAtInFuture()),
            ExerciseSetLogStoreResult.Status.PerformedAtOutsidePlan =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.PerformedAtOutsidePlan()),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
