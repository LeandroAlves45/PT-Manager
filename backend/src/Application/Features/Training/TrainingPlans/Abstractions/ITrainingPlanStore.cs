namespace Application.Features.Training.TrainingPlans.Abstractions;

/// <summary>Executa escritas compostas do agregado de plano de treino.</summary>
public interface ITrainingPlanStore
{
    Task<TrainingPlanStoreResult> CreateAsync(
        Guid trainerId,
        CreateTrainingPlanWriteModel model,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<TrainingPlanStoreResult> UpdateMetadataAsync(
        Guid trainerId,
        UpdateTrainingPlanMetadataWriteModel model,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<TrainingPlanStoreResult> UpdateStructureAsync(
        Guid trainerId,
        UpdateTrainingPlanStructureWriteModel model,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<TrainingPlanStoreResult> ArchiveAsync(
        Guid trainingPlanId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<TrainingPlanStoreResult> ReplaceAsync(
        Guid trainerId,
        ReplaceTrainingPlanWriteModel model,
        DateTime now,
        CancellationToken cancellationToken
    );
}
