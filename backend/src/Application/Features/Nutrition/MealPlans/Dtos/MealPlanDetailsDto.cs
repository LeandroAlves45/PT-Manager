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

    /// <summary>Alimento preescrito e respetiva contribuição nutricional.</summary>
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
        NutritionTotalsDto Contribution
    );

    /// <summary>Suplemento associado sem contribuição para totais nutricionais.</summary>
    public sealed record MealSupplementDto(
        Guid Id,
        Guid SupplementId,
        string SupplementName,
        string UnitOfMeasure,
        string? Notes,
        decimal Quantity,
        int OrderNumber
    );
}
