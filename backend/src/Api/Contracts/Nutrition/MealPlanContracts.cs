using Application.Features.Nutrition.MealPlans;
using Application.Features.Nutrition.MealPlans.Dtos;

namespace Api.Contracts.Nutrition;

/// <summary>Refeição pedida. Identificador nulo cria; identificador presente reconcilia.</summary>
public sealed record MealRequest(
    Guid? Id,
    string MealType,
    int OrderNumber,
    IReadOnlyList<MealItemRequest> Items,
    IReadOnlyList<MealSupplementRequest> Supplements);

/// <summary>Alimento prescrito numa refeição, em gramas.</summary>
public sealed record MealItemRequest(
    Guid? Id,
    Guid FoodId,
    decimal QuantityInGrams,
    int OrderNumber);

/// <summary>Suplemento associado a uma refeição, sem contribuição nutricional.</summary>
public sealed record MealSupplementRequest(
    Guid? Id,
    Guid SupplementId,
    string? Notes,
    decimal Quantity,
    int OrderNumber);

/// <summary>Estrutura completa desejada depois da escrita.</summary>
public sealed record MealPlanStructureRequest(IReadOnlyList<MealRequest> Meals)
{
    /// <summary>Converte a estrutura do contrato na entrada da Application.</summary>
    public MealPlanStructureInput ToInput()
    {
        ArgumentNullException.ThrowIfNull(Meals);

        return new MealPlanStructureInput(
            Meals.Select(meal => new MealPlanStructureInput.MealInput(
                meal.Id,
                meal.MealType,
                meal.OrderNumber,
                meal.Items
                    .Select(item => new MealPlanStructureInput.ItemInput(
                        item.Id,
                        item.FoodId,
                        item.QuantityInGrams,
                        item.OrderNumber))
                    .ToArray(),
                meal.Supplements
                    .Select(supplement => new MealPlanStructureInput.SupplementInput(
                        supplement.Id,
                        supplement.SupplementId,
                        supplement.Notes,
                        supplement.Quantity,
                        supplement.OrderNumber))
                    .ToArray()))
                .ToArray());
    }
}

/// <summary>Cria um plano alimentar e a respetiva árvore inicial.</summary>
public sealed record CreateMealPlanRequest(
    Guid ClientId,
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    NutritionCalculationRequest Calculation,
    MealPlanStructureRequest Structure);

/// <summary>Reconcilia um plano existente. Cálculo nulo preserva o cálculo já persistido.</summary>
public sealed record UpdateMealPlanRequest(
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    NutritionCalculationRequest? Calculation,
    MealPlanStructureRequest Structure);

/// <summary>Energia e nutrientes agregados.</summary>
public sealed record NutritionTotalsResponse(
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatsGrams,
    decimal Kcal,
    decimal FiberGrams)
{
    /// <summary>Projeta os totais da Application.</summary>
    public static NutritionTotalsResponse From(NutritionTotalsDto totals)
    {
        ArgumentNullException.ThrowIfNull(totals);

        return new(
            totals.ProteinGrams,
            totals.CarbsGrams,
            totals.FatsGrams,
            totals.Kcal,
            totals.FiberGrams
        );
    }
}

/// <summary>Alimento prescrito e a sua contribuição efetiva.</summary>
public sealed record MealItemResponse(
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
    NutritionTotalsResponse Contribution)
{
    /// <summary>Projeta o item da Application.</summary>
    public static MealItemResponse From(MealPlanDetailsDto.MealItemDto item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new(
            item.Id,
            item.FoodId,
            item.FoodName,
            item.QuantityInGrams,
            item.OrderNumber,
            item.ProteinPer100G,
            item.CarbsPer100G,
            item.FatsPer100G,
            item.KcalPer100G,
            item.FiberPer100G,
            NutritionTotalsResponse.From(item.Contribution)
        );
    }
}

/// <summary>Suplemento associado a uma refeição.</summary>
public sealed record MealSupplementResponse(
    Guid Id,
    Guid SupplementId,
    string SupplementName,
    string UnitOfMeasure,
    string? Notes,
    decimal Quantity,
    int OrderNumber)
{
    /// <summary>Projeta a associação da Application.</summary>
    public static MealSupplementResponse From(MealPlanDetailsDto.MealSupplementDto supplement)
    {
        ArgumentNullException.ThrowIfNull(supplement);

        return new(
            supplement.Id,
            supplement.SupplementId,
            supplement.SupplementName,
            supplement.UnitOfMeasure,
            supplement.Notes,
            supplement.Quantity,
            supplement.OrderNumber
        );
    }
}

/// <summary>Refeição ordenada com os respetivos totais.</summary>
public sealed record MealResponse(
    Guid Id,
    string MealType,
    int OrderNumber,
    NutritionTotalsResponse Totals,
    IReadOnlyList<MealItemResponse> Items,
    IReadOnlyList<MealSupplementResponse> Supplements)
{
    /// <summary>Projeta a refeição da Application.</summary>
    public static MealResponse From(MealPlanDetailsDto.MealDto meal)
    {
        ArgumentNullException.ThrowIfNull(meal);

        return new(
            meal.Id,
            meal.MealType,
            meal.OrderNumber,
            NutritionTotalsResponse.From(meal.Totals),
            meal.Items.Select(MealItemResponse.From).ToArray(),
            meal.Supplements.Select(MealSupplementResponse.From).ToArray()
        );
    }
}

/// <summary>Plano alimentar completo, na perspectiva do personal trainer.</summary>
public sealed record MealPlanDetailsResponse(
    Guid Id,
    Guid ClientId,
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    NutritionCalculationResponse Calculation,
    NutritionTotalsResponse ActualTotals,
    bool IsActive,
    bool IsArchived,
    bool NeedsReview,
    IReadOnlyList<MealResponse> Meals,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o detalhe da Application no contrato da API.</summary>
    public static MealPlanDetailsResponse From(MealPlanDetailsDto plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new(
            plan.Id,
            plan.ClientId,
            plan.Name,
            plan.Description,
            plan.StartsDate,
            plan.EndsDate,
            NutritionCalculationResponse.From(plan.Calculation),
            NutritionTotalsResponse.From(plan.ActualTotals),
            plan.IsActive,
            plan.IsArchived,
            plan.NeedsReview,
            plan.Meals.Select(MealResponse.From).ToArray(),
            plan.CreatedAt,
            plan.UpdatedAt
        );
    }
}

/// <summary>Resumo de plano alimentar para a listagem.</summary>
public sealed record MealPlanSummaryResponse(
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
    DateTime UpdatedAt)
{
    /// <summary>Projeta o resumo da Application no contrato da API.</summary>
    public static MealPlanSummaryResponse From(MealPlanSummaryDto plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new(
            plan.Id,
            plan.ClientId,
            plan.Name,
            plan.Description,
            plan.StartsDate,
            plan.EndsDate,
            plan.KcalTarget,
            plan.ProteinTargetGrams,
            plan.CarbsTargetGrams,
            plan.FatsTargetGrams,
            plan.IsActive,
            plan.IsArchived,
            plan.NeedsReview,
            plan.CreatedAt,
            plan.UpdatedAt
        );
    }
}
