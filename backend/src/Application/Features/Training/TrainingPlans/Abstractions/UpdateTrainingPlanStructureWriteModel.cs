namespace Application.Features.Training.TrainingPlans.Abstractions;

/// <summary>Transporta a estrutura final de um plano de treino existente.</summary>
public sealed record UpdateTrainingPlanStructureWriteModel(
    Guid TrainingPlanId,
    TrainingPlanStructureInput Structure
);
