using Application.Features.Training.TrainingPlans.Abstractions;
using Application.Features.Training.TrainingPlans.Dtos;
using Application.Results;

namespace Application.Features.Training.TrainingPlans;

/// <summary>Converte resultados de persistência de planos de treino em resultados da Application.</summary>
internal static class TrainingPlanStoreResultMapper
{
    internal static Result ToArchiveResult(this TrainingPlanStoreResult outcome) =>
        outcome.Kind switch
        {
            TrainingPlanStoreResult.Status.Changed or
            TrainingPlanStoreResult.Status.AlreadyArchived => Result.Success(),
            TrainingPlanStoreResult.Status.NotFound =>
                Result.Failure(TrainingErrors.TrainingPlanNotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    internal static Result<TrainingPlanDetailsDto> ToDetailsFailure(
        this TrainingPlanStoreResult outcome) =>
        outcome.Kind switch
        {
            TrainingPlanStoreResult.Status.NotFound =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.TrainingPlanNotFound),
            TrainingPlanStoreResult.Status.Inactive =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.TrainingPlanInactive),
            TrainingPlanStoreResult.Status.ClientNotFound =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.ClientNotFound),
            TrainingPlanStoreResult.Status.ExerciseReferenceNotFound =>
                Result<TrainingPlanDetailsDto>.Failure(
                    TrainingErrors.ExerciseReferenceNotFound),
            TrainingPlanStoreResult.Status.ExerciseReferenceInactive =>
                Result<TrainingPlanDetailsDto>.Failure(
                    TrainingErrors.ExerciseReferenceInactive),
            TrainingPlanStoreResult.Status.StructureReferenceNotFound =>
                Result<TrainingPlanDetailsDto>.Failure(
                    TrainingErrors.StructureReferenceNotFound),
            TrainingPlanStoreResult.Status.StructureHasHistory =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.StructureHasHistory),
            TrainingPlanStoreResult.Status.StructureReorderRequiresFreeSlot =>
                Result<TrainingPlanDetailsDto>.Failure(
                    TrainingErrors.StructureReorderRequiresFreeSlot),
            TrainingPlanStoreResult.Status.ActivePlanConflict =>
                Result<TrainingPlanDetailsDto>.Failure(
                    TrainingErrors.ActiveTrainingPlanConflict),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
