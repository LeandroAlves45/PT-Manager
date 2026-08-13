namespace Application.Features.Training.TrainingPlans.ListTrainingPlans;

/// <summary>Solicita uma página de planos visíveis.</summary>
public sealed record ListTrainingPlansQuery(
    Guid? ClientId = null,
    string? Search = null,
    TrainingPlanActivityFilter Activity = TrainingPlanActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50
);
