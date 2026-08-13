namespace Application.Features.Training.TrainingPlans.ArchiveTrainingPlan;

/// <summary>Solicita o arquivo idempotente de um plano de treino.</summary>
public sealed record ArchiveTrainingPlanCommand(Guid TrainingPlanId);
