using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>
/// Projeta o plano alimentar ativo do cliente autenticado.
/// </summary>
internal sealed class MyNutritionPlanQueries : IMyNutritionPlanQueries
{
    private readonly PtManagerDbContext _dbContext;

    public MyNutritionPlanQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<MyNutritionPlanDto?> GetActiveAsync(
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken)
    {
        var plan = await _dbContext.MealPlans
            .AsNoTracking()
            .AsSingleQuery()
            .Where(candidate =>
                candidate.OwnerTrainerId == trainerId &&
                candidate.IsActive &&
                !candidate.IsArchived &&
                _dbContext.Clients.Any(client =>
                    client.Id == candidate.ClientId &&
                    client.OwnerTrainerId == trainerId &&
                    client.UserId == clientUserId &&
                    client.IsActive))
            .OrderByDescending(candidate => candidate.StartsDate)
            .ThenByDescending(candidate => candidate.CreatedAt)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => new PlanRow(
                candidate.Id,
                candidate.Name,
                candidate.Description,
                candidate.StartsDate,
                candidate.EndsDate,
                candidate.KcalTarget,
                candidate.Targets.ProteinGrams,
                candidate.Targets.CarbsGrams,
                candidate.Targets.FatsGrams,
                candidate.UpdatedAt,
                _dbContext.MealPlanMeals
                    .Where(meal => meal.MealPlanId == candidate.Id)
                    .OrderBy(meal => meal.OrderNumber)
                    .ThenBy(meal => meal.Id)
                    .Select(meal => new MealRow(
                        meal.MealType,
                        meal.OrderNumber,
                        _dbContext.MealPlanMealItems
                            .Where(item => item.MealPlanMealId == meal.Id)
                            .Join(
                                _dbContext.Foods,
                                item => item.FoodId,
                                food => food.Id,
                                (item, food) => new { item, food })
                            .OrderBy(row => row.item.OrderNumber)
                            .ThenBy(row => row.item.Id)
                            .Select(row => new ItemRow(
                                row.item.OrderNumber,
                                row.food.PlatformEnforcementStatus ==
                                    PlatformEnforcementStatus.Blocked
                                    ? PortalContentMasking.UnavailableFoodName
                                    : row.food.Name,
                                row.food.PlatformEnforcementStatus ==
                                    PlatformEnforcementStatus.Blocked,
                                row.item.QuantityInGrams,
                                row.food.Protein,
                                row.food.Carbs,
                                row.food.Fats,
                                row.food.Kcal,
                                row.food.Fiber))
                            .ToList(),
                        _dbContext.MealPlanMealSupplements
                            .Where(link => link.MealPlanMealId == meal.Id)
                            .Join(
                                _dbContext.Supplements,
                                link => link.SupplementId,
                                supplement => supplement.Id,
                                (link, supplement) => new { link, supplement })
                            .OrderBy(row => row.link.OrderNumber)
                            .ThenBy(row => row.link.Id)
                            .Select(row => new MyNutritionPlanDto.SupplementDto(
                                row.link.OrderNumber,
                                row.supplement.Name,
                                !row.supplement.IsActive,
                                row.supplement.UnitOfMeasure,
                                row.link.Quantity,
                                row.link.Notes))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        if (plan is null)
            return null;

        var meals = plan.Meals
            .Select(meal => new MyNutritionPlanDto.MealDto(
                meal.MealType,
                meal.OrderNumber,
                CalculateTotals(meal.Items),
                meal.Items
                    .Select(item => new MyNutritionPlanDto.ItemDto(
                        item.OrderNumber,
                        item.FoodName,
                        item.IsUnavailable,
                        item.QuantityInGrams,
                        CalculateContribution(item)))
                    .ToArray(),
                meal.Supplements))
            .ToArray();

        return new MyNutritionPlanDto(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.StartsDate,
            plan.EndsDate,
            plan.KcalTarget,
            plan.ProteinTargetGrams,
            plan.CarbsTargetGrams,
            plan.FatsTargetGrams,
            CalculateTotals(plan.Meals.SelectMany(meal => meal.Items)),
            meals,
            plan.UpdatedAt);
    }

    /// <summary>Contribuição de um item, proporcional aos gramas prescritos.</summary>
    private static MyNutritionPlanDto.TotalsDto CalculateContribution(ItemRow item) => new(
        Round(item.Protein * item.QuantityInGrams / 100m),
        Round(item.Carbs * item.QuantityInGrams / 100m),
        Round(item.Fats * item.QuantityInGrams / 100m),
        Round(item.Kcal * item.QuantityInGrams / 100m),
        Round((item.Fiber ?? 0m) * item.QuantityInGrams / 100m));

    /// <summary>Soma as contribuições de um conjunto de itens.</summary>
    private static MyNutritionPlanDto.TotalsDto CalculateTotals(IEnumerable<ItemRow> items)
    {
        var materialized = items as IReadOnlyCollection<ItemRow> ?? items.ToArray();
        return new MyNutritionPlanDto.TotalsDto(
            Round(materialized.Sum(item => item.Protein * item.QuantityInGrams / 100m)),
            Round(materialized.Sum(item => item.Carbs * item.QuantityInGrams / 100m)),
            Round(materialized.Sum(item => item.Fats * item.QuantityInGrams / 100m)),
            Round(materialized.Sum(item => item.Kcal * item.QuantityInGrams / 100m)),
            Round(materialized.Sum(item =>
                (item.Fiber ?? 0m) * item.QuantityInGrams / 100m)));
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Linha do plano tal como sai da projecção.</summary>
    private sealed record PlanRow(
        Guid Id,
        string Name,
        string? Description,
        DateOnly StartsDate,
        DateOnly? EndsDate,
        decimal KcalTarget,
        decimal ProteinTargetGrams,
        decimal CarbsTargetGrams,
        decimal FatsTargetGrams,
        DateTime UpdatedAt,
        IReadOnlyList<MealRow> Meals);

    /// <summary>Linha de refeição, antes de os totais serem calculados.</summary>
    private sealed record MealRow(
        string MealType,
        int OrderNumber,
        IReadOnlyList<ItemRow> Items,
        IReadOnlyList<MyNutritionPlanDto.SupplementDto> Supplements);

    /// <summary>
    /// Linha de item com os macros por 100g do alimento, necessários ao cálculo.
    /// </summary>
    private sealed record ItemRow(
        int OrderNumber,
        string FoodName,
        bool IsUnavailable,
        decimal QuantityInGrams,
        decimal Protein,
        decimal Carbs,
        decimal Fats,
        decimal Kcal,
        decimal? Fiber);
}
