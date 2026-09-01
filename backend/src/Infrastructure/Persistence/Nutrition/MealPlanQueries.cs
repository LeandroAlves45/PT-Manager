using Application.Features.Nutrition.Calculations;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Application.Features.Nutrition.MealPlans.Dtos;
using Application.Features.Nutrition.MealPlans.ListMealPlans;
using Application.Pagination;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Nutrition;

/// <summary>Executa leituras projetadas e cálculos efetivos de planos alimentares.</summary>
internal sealed class MealPlanQueries : IMealPlanQueries
{
    private readonly PtManagerDbContext _dbContext;

    public MealPlanQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<MealPlanDetailsDto?> GetDetailsAsync(
        Guid mealPlanId,
        CancellationToken cancellationToken
    )
    {
        var plan = await _dbContext.MealPlans
            .AsNoTracking()
            .Where(candidate => candidate.Id == mealPlanId)
            .Select(candidate => new PlanRow(
                candidate.Id,
                candidate.ClientId,
                candidate.Name,
                candidate.Description,
                candidate.StartsDate,
                candidate.EndsDate,
                candidate.CalculationSnapshot,
                candidate.IsActive,
                candidate.IsArchived,
                candidate.CreatedAt,
                candidate.UpdatedAt
            ))
            .SingleOrDefaultAsync(cancellationToken);
        if (plan is null)
            return null;

        var meals = await _dbContext.MealPlanMeals
            .AsNoTracking()
            .Where(meal => meal.MealPlanId == mealPlanId)
            .OrderBy(meal => meal.OrderNumber)
            .ThenBy(meal => meal.Id)
            .Select(meal => new MealRow(meal.Id, meal.MealType, meal.OrderNumber))
            .ToListAsync(cancellationToken);
        var mealIds = meals.Select(meal => meal.Id).ToArray();

        var items = await _dbContext.MealPlanMealItems
            .AsNoTracking()
            .Where(item => mealIds.Contains(item.MealPlanMealId))
            .Join(
                _dbContext.Foods.AsNoTracking(),
                item => item.FoodId,
                food => food.Id,
                (item, food) => new { Outer = item, Inner = food }
            )
            .OrderBy(item => item.Outer.OrderNumber)
            .ThenBy(item => item.Outer.Id)
            .Select(item => new MealItemRow(
                item.Outer.Id,
                item.Outer.MealPlanMealId,
                item.Outer.FoodId,
                item.Inner.Name,
                item.Outer.QuantityInGrams,
                item.Outer.OrderNumber,
                item.Inner.Protein,
                item.Inner.Carbs,
                item.Inner.Fats,
                item.Inner.Kcal,
                item.Inner.Fiber,
                item.Inner.PlatformEnforcementStatus ==
                    Domain.ValueObjects.PlatformEnforcementStatus.Blocked
            ))
            .ToListAsync(cancellationToken);

        var supplements = await _dbContext.MealPlanMealSupplements
            .AsNoTracking()
            .Where(item => mealIds.Contains(item.MealPlanMealId))
            .Join(
                _dbContext.Supplements.AsNoTracking(),
                association => association.SupplementId,
                supplement => supplement.Id,
                (association, supplement) => new
                {
                    Outer = association,
                    Inner = supplement
                }
            )
            .OrderBy(item => item.Outer.OrderNumber)
            .ThenBy(item => item.Outer.Id)
            .Select(item => new SupplementRow(
                item.Outer.Id,
                item.Outer.MealPlanMealId,
                item.Outer.SupplementId,
                item.Inner.Name,
                item.Inner.UnitOfMeasure,
                item.Outer.Notes,
                item.Outer.Quantity,
                item.Outer.OrderNumber
            ))
            .ToListAsync(cancellationToken);

        var itemsDtos = items.ToDictionary(
            item => item.Id,
            item => new MealPlanDetailsDto.MealItemDto(
                item.Id,
                item.FoodId,
                item.FoodName,
                item.QuantityInGrams,
                item.OrderNumber,
                item.Protein,
                item.Carbs,
                item.Fats,
                item.Kcal,
                item.Fiber,
                CalculateContribution(item)
            )
        );

        var mealDtos = meals.Select(meal =>
        {
            var mealItems = items.Where(item => item.MealPlanMealId == meal.Id).ToArray();
            return new MealPlanDetailsDto.MealDto(
                meal.Id,
                meal.MealType,
                meal.OrderNumber,
                CalculateTotals(mealItems),
                mealItems.Select(item => itemsDtos[item.Id])
                    .OrderBy(item => item.OrderNumber).ThenBy(item => item.Id).ToArray(),
                supplements.Where(item => item.MealPlanMealId == meal.Id)
                    .OrderBy(item => item.OrderNumber).ThenBy(item => item.Id)
                    .Select(item => new MealPlanDetailsDto.MealSupplementDto(
                        item.Id,
                        item.SupplementId,
                        item.SupplementName,
                        item.UnitOfMeasure,
                        item.Notes,
                        item.Quantity,
                        item.OrderNumber
                    )).ToArray()
            );
        }).ToArray();

        return new MealPlanDetailsDto(
            plan.Id,
            plan.ClientId,
            plan.Name,
            plan.Description,
            plan.StartsDate,
            plan.EndsDate,
            MapSnapshot(plan.CalculationSnapshot),
            CalculateTotals(items),
            plan.IsActive,
            plan.IsArchived,
            items.Any(item => item.IsPlatformBlocked),
            mealDtos,
            plan.CreatedAt,
            plan.UpdatedAt
        );
    }

    public async Task<PageResult<MealPlanSummaryDto>> ListAsync(
        Guid? clientId,
        string? search,
        MealPlanActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var query = _dbContext.MealPlans.AsNoTracking();
        if (clientId.HasValue)
            query = query.Where(plan => plan.ClientId == clientId.Value);

        query = activity switch
        {
            MealPlanActivityFilter.Active => query.Where(plan => plan.IsActive && !plan.IsArchived),
            MealPlanActivityFilter.Archived => query.Where(plan => plan.IsArchived),
            MealPlanActivityFilter.All => query,
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikeSearchPattern.Build(search);
            query = query.Where(plan =>
                EF.Functions.ILike(
                    plan.Name, pattern, LikeSearchPattern.LikeEscapeCharacter)
                || plan.Description != null
                    && EF.Functions.ILike(
                        plan.Description, pattern, LikeSearchPattern.LikeEscapeCharacter)
            );
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(plan => plan.StartsDate)
            .ThenByDescending(plan => plan.CreatedAt)
            .ThenBy(plan => plan.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(plan => new MealPlanSummaryDto(
                plan.Id,
                plan.ClientId,
                plan.Name,
                plan.Description,
                plan.StartsDate,
                plan.EndsDate,
                plan.KcalTarget,
                plan.Targets.ProteinGrams,
                plan.Targets.CarbsGrams,
                plan.Targets.FatsGrams,
                plan.IsActive,
                plan.IsArchived,
                _dbContext.MealPlanMealItems.Any(item =>
                    _dbContext.MealPlanMeals.Any(meal =>
                        meal.Id == item.MealPlanMealId && meal.MealPlanId == plan.Id) &&
                    _dbContext.Foods.Any(food =>
                        food.Id == item.FoodId &&
                        food.PlatformEnforcementStatus ==
                            Domain.ValueObjects.PlatformEnforcementStatus.Blocked)),
                plan.CreatedAt,
                plan.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PageResult<MealPlanSummaryDto>(items, totalCount);
    }

    private static NutritionTotalsDto CalculateContribution(MealItemRow item) => new(
        Round(item.Protein * item.QuantityInGrams / 100m),
        Round(item.Carbs * item.QuantityInGrams / 100m),
        Round(item.Fats * item.QuantityInGrams / 100m),
        Round(item.Kcal * item.QuantityInGrams / 100m),
        Round((item.Fiber ?? 0m) * item.QuantityInGrams / 100m)
    );

    private static NutritionTotalsDto CalculateTotals(IEnumerable<MealItemRow> items)
    {
        var materialized = items.ToArray();
        return new NutritionTotalsDto(
            Round(materialized.Sum(item => item.Protein * item.QuantityInGrams / 100m)),
            Round(materialized.Sum(item => item.Carbs * item.QuantityInGrams / 100m)),
            Round(materialized.Sum(item => item.Fats * item.QuantityInGrams / 100m)),
            Round(materialized.Sum(item => item.Kcal * item.QuantityInGrams / 100m)),
            Round(materialized.Sum(item => (item.Fiber ?? 0m) * item.QuantityInGrams / 100m))
        );
    }

    private static NutritionCalculationDto MapSnapshot(
        Domain.ValueObjects.NutritionCalculationSnapshot snapshot
    ) => new(
        snapshot.SchemaVersion,
        snapshot.CalculationOrigin,
        snapshot.CalculatedAt,
        snapshot.EnergyFormula,
        snapshot.WeightKgUsed,
        snapshot.HeightCmUsed,
        snapshot.AgeUsed,
        snapshot.SexUsed,
        snapshot.BodyFatPercentageUsed,
        snapshot.ActivityLevel,
        snapshot.ActivityFactor,
        snapshot.GoalType,
        snapshot.GoalAdjustmentKcal,
        snapshot.RestingEnergyExpenditureKcal,
        snapshot.TotalDailyEnergyExpenditureKcal,
        snapshot.TargetKcal,
        snapshot.MacroDistributionMode,
        snapshot.ProteinTargetGrams,
        snapshot.CarbsTargetGrams,
        snapshot.FatsTargetGrams,
        snapshot.ProteinEnergyPercentage,
        snapshot.CarbsEnergyPercentage,
        snapshot.FatsEnergyPercentage,
        snapshot.CalculatedMacroKcal,
        snapshot.KcalDifference
    );

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record PlanRow(
        Guid Id,
        Guid ClientId,
        string Name,
        string? Description,
        DateOnly StartsDate,
        DateOnly? EndsDate,
        Domain.ValueObjects.NutritionCalculationSnapshot CalculationSnapshot,
        bool IsActive,
        bool IsArchived,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );

    private sealed record MealRow(Guid Id, string MealType, int OrderNumber);
    private sealed record MealItemRow(
        Guid Id,
        Guid MealPlanMealId,
        Guid FoodId,
        string FoodName,
        decimal QuantityInGrams,
        int OrderNumber,
        decimal Protein,
        decimal Carbs,
        decimal Fats,
        decimal Kcal,
        decimal? Fiber,
        bool IsPlatformBlocked
    );

    private sealed record SupplementRow(
        Guid Id,
        Guid MealPlanMealId,
        Guid SupplementId,
        string SupplementName,
        string UnitOfMeasure,
        string? Notes,
        decimal Quantity,
        int OrderNumber
    );
}
