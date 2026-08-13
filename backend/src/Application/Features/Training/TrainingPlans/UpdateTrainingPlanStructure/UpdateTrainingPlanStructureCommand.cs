namespace Application.Features.Training.TrainingPlans.UpdateTrainingPlanStructure;

/// <summary>Solicita a reconciliação da estrutura final do mesmo plano de treino.</summary>
public sealed record UpdateTrainingPlanStructureCommand(
    Guid TrainingPlanId,
    TrainingPlanStructureInput Structure
);
