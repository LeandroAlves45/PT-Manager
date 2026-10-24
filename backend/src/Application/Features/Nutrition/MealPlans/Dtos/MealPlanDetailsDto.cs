using Application.Common;
using Application.Features.Nutrition.Calculations;

namespace Application.Features.Nutrition.MealPlans.Dtos;

/// <summary>Representa um plano alimentar completo e a composição alimentar efetiva.</summary>
public sealed record MealPlanDetailsDto(
    Guid Id,
    Guid ClientId,
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    NutritionCalculationDto Calculation,
    NutritionTotalsDto ActualTotals,
    bool IsActive,
    bool IsArchived,
    bool NeedsReview,
    IReadOnlyList<MealPlanDetailsDto.MealDto> Meals,
    IReadOnlyList<MealPlanDetailsDto.MealSupplementDto> PlanSupplements,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    /// <summary>Refeição ordenada e respetivos totais.</summary>
    public sealed record MealDto(
        Guid Id,
        string MealType,
        int OrderNumber,
        NutritionTotalsDto Totals,
        IReadOnlyList<MealItemDto> Items,
        IReadOnlyList<MealSupplementDto> Supplements
    );

    /// <summary>
    /// Alimento prescrito e respetiva contribuição nutricional. O bloqueado pela moderação traz
    /// o motivo e a nota e não conta nos totais da refeição nem do plano.
    /// </summary>
    public sealed record MealItemDto(
        Guid Id,
        Guid FoodId,
        string FoodName,
        decimal QuantityInGrams,
        int OrderNumber,
        decimal ProteinPer100G,
        decimal CarbsPer100G,
        decimal FatsPer100G,
        decimal KcalPer100G,
        decimal? FiberPer100G,
        NutritionTotalsDto Contribution,
        PlatformEnforcementDto? PlatformEnforcement
    );

    /// <summary>Suplemento associado sem contribuição para totais nutricionais; o bloqueado traz o motivo e a nota.</summary>
    public sealed record MealSupplementDto(
        Guid Id,
        Guid SupplementId,
        string SupplementName,
        string UnitOfMeasure,
        string? Notes,
        decimal Quantity,
        int OrderNumber,
        PlatformEnforcementDto? PlatformEnforcement
    );
}
