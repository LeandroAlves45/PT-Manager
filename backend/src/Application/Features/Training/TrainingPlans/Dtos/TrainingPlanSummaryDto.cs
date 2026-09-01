namespace Application.Features.Training.TrainingPlans.Dtos;

/// <summary>Resume um plano de treino sem carregar a árvore.</summary>
public sealed record TrainingPlanSummaryDto(
    Guid Id,
    Guid ClientId,
    string Name,
    string? Description,
    string? TrainingModality,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    bool IsArchived,
    bool NeedsReview,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
