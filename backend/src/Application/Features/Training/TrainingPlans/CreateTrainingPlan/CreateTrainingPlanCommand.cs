namespace Application.Features.Training.TrainingPlans.CreateTrainingPlan;

/// <summary>Solicita a criação de um plano de treino e árvore integral.</summary>
public sealed record CreateTrainingPlanCommand(
    Guid ClientId,
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate,
    TrainingPlanStructureInput Structure
);
