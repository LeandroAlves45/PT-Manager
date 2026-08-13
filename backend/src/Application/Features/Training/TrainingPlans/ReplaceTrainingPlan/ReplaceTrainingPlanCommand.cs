namespace Application.Features.Training.TrainingPlans.ReplaceTrainingPlan;

/// <summary>Solicita o arquivo do plano atual e criação de um novo plano de treino.</summary>
public sealed record ReplaceTrainingPlanCommand(
    Guid TrainingPlanId,
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate,
    TrainingPlanStructureInput Structure
);
