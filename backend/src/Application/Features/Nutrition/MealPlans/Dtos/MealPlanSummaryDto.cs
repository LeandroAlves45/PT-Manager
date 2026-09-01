namespace Application.Features.Nutrition.MealPlans.Dtos;

/// <summary>Resume um plano alimentar sem carregar a árvore.</summary>
public sealed record MealPlanSummaryDto(
    Guid Id,
    Guid ClientId,
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    decimal KcalTarget,
    decimal ProteinTargetGrams,
    decimal CarbsTargetGrams,
    decimal FatsTargetGrams,
    bool IsActive,
    bool IsArchived,
    bool NeedsReview,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
