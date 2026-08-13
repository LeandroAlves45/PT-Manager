namespace Application.Features.Training.TrainingPlans.UpdateTrainingPlanMetadata;

/// <summary>Solicita a atualização integral dos metadados de um plano de treino.</summary>
public sealed record UpdateTrainingPlanMetadataCommand(
    Guid TrainingPlanId,
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate
);
