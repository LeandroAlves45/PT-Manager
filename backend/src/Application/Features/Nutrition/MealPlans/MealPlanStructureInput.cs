namespace Application.Features.Nutrition.MealPlans;

/// <summary>
/// Representa todas as refeições e os suplementos fora das refeições desejados depois da
/// escrita. Uma lista vazia de suplementos do plano retira os que existiam.
/// </summary>
public sealed record MealPlanStructureInput(
    IReadOnlyList<MealPlanStructureInput.MealInput> Meals,
    IReadOnlyList<MealPlanStructureInput.SupplementInput> PlanSupplements
)
{
    /// <summary>Refeição existente quando o Id tem valor; nova quando é null.</summary>
    public sealed record MealInput(
        Guid? Id,
        string MealType,
        int OrderNumber,
        IReadOnlyList<ItemInput> Items,
        IReadOnlyList<SupplementInput> Supplements
    );

    /// <summary>Item existente quando o Id tem valor; novo quando é null.</summary>
    public sealed record ItemInput(
        Guid? Id,
        Guid FoodId,
        decimal QuantityInGrams,
        int OrderNumber
    );

    /// <summary>Associação existente quando o Id tem valor; nova quando é null.</summary>
    public sealed record SupplementInput(
        Guid? Id,
        Guid SupplementId,
        string? Notes,
        decimal Quantity,
        int OrderNumber
    );
}
