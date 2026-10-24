using Application.Features.ClientPortal.PlanSupplementIntakes;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>
/// Lê os suplementos do plano alimentar escolhido e as tomas de um dia. Parte sempre de um
/// cliente ativo; um plano arquivado nunca está escolhido.
/// </summary>
internal sealed class MyPlanSupplementIntakeQueries : IMyPlanSupplementIntakeQueries
{
    internal const string MealPlacement = "meal";
    internal const string PlanPlacement = "plan";

    private readonly PtManagerDbContext _dbContext;

    public MyPlanSupplementIntakeQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<MyPlanSupplementsRead> GetForDateAsync(
        Guid trainerId,
        Guid clientUserId,
        DateOnly localDate,
        CancellationToken cancellationToken)
    {
        var client = await _dbContext.Clients
            .AsNoTracking()
            .Where(item => item.OwnerTrainerId == trainerId && item.UserId == clientUserId && item.IsActive)
            .Select(item => new { item.Id, item.SelectedMealPlanId })
            .SingleOrDefaultAsync(cancellationToken);
        if (client is null)
            return new MyPlanSupplementsRead(MyPlanSupplementsReadStatus.ProfileNotAvailable, null);

        var plan = await _dbContext.MealPlans
            .AsNoTracking()
            .Where(item =>
                item.Id == client.SelectedMealPlanId &&
                item.OwnerTrainerId == trainerId &&
                item.ClientId == client.Id &&
                item.IsActive &&
                !item.IsArchived)
            .Select(item => new { item.Id, item.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (plan is null)
        {
            var hasActivePlans = await _dbContext.MealPlans
                .AsNoTracking()
                .AnyAsync(item =>
                    item.OwnerTrainerId == trainerId &&
                    item.ClientId == client.Id &&
                    item.IsActive &&
                    !item.IsArchived,
                    cancellationToken);
            return new MyPlanSupplementsRead(
                hasActivePlans
                    ? MyPlanSupplementsReadStatus.PlanNotSelected
                    : MyPlanSupplementsReadStatus.PlanNotAvailable,
                null);
        }

        var inMeals = await (
                from link in _dbContext.MealPlanMealSupplements.AsNoTracking()
                join meal in _dbContext.MealPlanMeals.AsNoTracking() on link.MealPlanMealId equals meal.Id
                join supplement in _dbContext.Supplements.AsNoTracking() on link.SupplementId equals supplement.Id
                // O suplemento bloqueado pela moderação sai do plano do cliente.
                where meal.MealPlanId == plan.Id &&
                    supplement.PlatformEnforcementStatus != Domain.ValueObjects.PlatformEnforcementStatus.Blocked
                orderby meal.OrderNumber, meal.Id, link.OrderNumber, link.Id
                select new MyPlanSupplementItemDto(
                    link.Id,
                    MealPlacement,
                    meal.MealType,
                    meal.OrderNumber,
                    link.OrderNumber,
                    supplement.Name,
                    supplement.UnitOfMeasure,
                    link.Quantity,
                    link.Notes,
                    !supplement.IsActive,
                    _dbContext.ClientMealPlanSupplementIntakes
                        .Where(intake =>
                            intake.MealPlanMealSupplementId == link.Id &&
                            intake.ClientId == client.Id &&
                            intake.LocalDate == localDate)
                        .Select(intake => (DateTime?)intake.TakenAt)
                        .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var outsideMeals = await (
                from item in _dbContext.MealPlanSupplements.AsNoTracking()
                join supplement in _dbContext.Supplements.AsNoTracking() on item.SupplementId equals supplement.Id
                where item.MealPlanId == plan.Id &&
                    supplement.PlatformEnforcementStatus != Domain.ValueObjects.PlatformEnforcementStatus.Blocked
                orderby item.OrderNumber, item.Id
                select new MyPlanSupplementItemDto(
                    item.Id,
                    PlanPlacement,
                    null,
                    null,
                    item.OrderNumber,
                    supplement.Name,
                    supplement.UnitOfMeasure,
                    item.Quantity,
                    item.Notes,
                    !supplement.IsActive,
                    _dbContext.ClientMealPlanSupplementIntakes
                        .Where(intake =>
                            intake.MealPlanSupplementId == item.Id &&
                            intake.ClientId == client.Id &&
                            intake.LocalDate == localDate)
                        .Select(intake => (DateTime?)intake.TakenAt)
                        .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new MyPlanSupplementsRead(
            MyPlanSupplementsReadStatus.Found,
            new MyPlanSupplementsTodayDto(plan.Id, plan.Name, localDate, [.. inMeals, .. outsideMeals]));
    }
}
