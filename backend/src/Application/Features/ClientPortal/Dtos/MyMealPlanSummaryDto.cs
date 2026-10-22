namespace Application.Features.ClientPortal.Dtos;

/// <summary>
/// Plano alimentar ativo do cliente na lista do portal. Mostra a meta de energia e a palavra do
/// objetivo (manutenção, défice ou superávit); a fórmula, a idade, o sexo e o ajuste ficam só
/// com o personal trainer.
/// </summary>
public sealed record MyMealPlanSummaryDto(
    Guid Id,
    string Name,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    decimal TargetKcal,
    string? GoalType,
    int MealCount,
    bool IsSelected);
