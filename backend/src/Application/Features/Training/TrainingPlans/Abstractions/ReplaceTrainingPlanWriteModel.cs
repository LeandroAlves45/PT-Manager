namespace Application.Features.Training.TrainingPlans.Abstractions;

/// <summary>Transporta o plano novo sem permitir substituir o cliente.</summary>
public sealed record ReplaceTrainingPlanWriteModel(
    Guid TrainingPlanId,
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate,
    TrainingPlanStructureInput Structure
);
