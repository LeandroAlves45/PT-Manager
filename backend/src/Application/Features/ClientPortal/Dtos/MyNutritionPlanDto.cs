namespace Application.Features.ClientPortal.Dtos;

/// <summary>
/// Plano alimentar ativo, na perspectiva do cliente. <see cref="ShowMacros"/> é a escolha do
/// personal trainer no plano; os gramas de macros só são null depois de <see cref="ForClient"/>.
/// </summary>
public sealed record MyNutritionPlanDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    decimal TargetKcal,
    decimal? ProteinTargetGrams,
    decimal? CarbsTargetGrams,
    decimal? FatsTargetGrams,
    string? GoalType,
    MyNutritionPlanDto.TotalsDto ActualTotals,
    IReadOnlyList<MyNutritionPlanDto.MealDto> Meals,
    IReadOnlyList<MyNutritionPlanDto.SupplementDto> PlanSupplements,
    DateTime UpdatedAt,
    bool ShowMacros)
{
    /// <summary>
    /// O que o cliente pode receber. Com as macros escondidas no plano, saem a null as metas e os
    /// gramas de proteína, hidratos, gordura e fibra de todos os totais; ficam só as kcal. Não
    /// basta esconder no ecrã: os valores ficariam visíveis na resposta.
    /// </summary>
    public MyNutritionPlanDto ForClient() => ShowMacros
        ? this
        : this with
        {
            ProteinTargetGrams = null,
            CarbsTargetGrams = null,
            FatsTargetGrams = null,
            ActualTotals = ActualTotals.KcalOnly(),
            Meals = Meals
                .Select(meal => meal with
                {
                    Totals = meal.Totals.KcalOnly(),
                    Items = meal.Items
                        .Select(item => item with { Contribution = item.Contribution.KcalOnly() })
                        .ToArray(),
                })
                .ToArray(),
        };

    /// <summary>Energia e nutrientes agregados. Os gramas são null quando o plano esconde as macros.</summary>
    public sealed record TotalsDto(
        decimal? ProteinGrams,
        decimal? CarbsGrams,
        decimal? FatsGrams,
        decimal Kcal,
        decimal? FiberGrams)
    {
        /// <summary>Os mesmos totais só com as kcal.</summary>
        public TotalsDto KcalOnly() => new(null, null, null, Kcal, null);
    }

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
