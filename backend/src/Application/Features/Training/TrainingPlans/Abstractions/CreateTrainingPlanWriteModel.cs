namespace Application.Features.Training.TrainingPlans.Abstractions;

/// <summary>Transporta uma criação completa para a transação persistente.</summary>
public sealed record CreateTrainingPlanWriteModel(
    Guid ClientId,
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate,
    TrainingPlanStructureInput Structure
);
