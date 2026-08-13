namespace Application.Features.Training.TrainingPlans.Abstractions;

/// <summary>Transporta uma atualização de metadados para o store.</summary>
public sealed record UpdateTrainingPlanMetadataWriteModel(
    Guid TrainingPlanId,
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate
);
