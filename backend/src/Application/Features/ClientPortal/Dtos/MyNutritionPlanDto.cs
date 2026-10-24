namespace Application.Features.ClientPortal.Dtos;

/// <summary>Plano alimentar ativo, na perspectiva do cliente.</summary>
public sealed record MyNutritionPlanDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    decimal TargetKcal,
    decimal ProteinTargetGrams,
    decimal CarbsTargetGrams,
    decimal FatsTargetGrams,
    string? GoalType,
    MyNutritionPlanDto.TotalsDto ActualTotals,
    IReadOnlyList<MyNutritionPlanDto.MealDto> Meals,
    IReadOnlyList<MyNutritionPlanDto.SupplementDto> PlanSupplements,
    DateTime UpdatedAt)
{
    /// <summary>Energia e nutrientes agregados.</summary>
    public sealed record TotalsDto(
        decimal ProteinGrams,
        decimal CarbsGrams,
        decimal FatsGrams,
        decimal Kcal,
        decimal FiberGrams);

    /// <summary>Refeição ordenada com os respectivos totais.</summary>
    public sealed record MealDto(
        string MealType,
        int OrderNumber,
        TotalsDto Totals,
        IReadOnlyList<ItemDto> Items,
        IReadOnlyList<SupplementDto> Supplements);

    /// <summary>Alimento prescrito; o bloqueado pela moderação não chega aqui nem conta nos totais.</summary>
    public sealed record ItemDto(
        int OrderNumber,
        string FoodName,
        decimal QuantityInGrams,
        TotalsDto Contribution);

    /// <summary>
    /// Suplemento da refeição ou do plano fora das refeições. O id é o da prescrição, o mesmo
    /// das tomas de hoje.
    /// </summary>
    public sealed record SupplementDto(
        Guid Id,
        int OrderNumber,
        string SupplementName,
        bool IsUnavailable,
        string UnitOfMeasure,
        decimal Quantity,
        string? Notes);
}
