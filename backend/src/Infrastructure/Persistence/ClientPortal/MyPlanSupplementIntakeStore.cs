using Application.Common.Abstractions;
using Application.Common.Time;
using Application.Features.ClientPortal.PlanSupplementIntakes;
using Domain.Entities.Supplements;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>
/// Escreve as tomas de hoje dos suplementos do plano escolhido. A ficha do cliente fica
/// bloqueada durante a escrita, para dois toques seguidos não criarem duas tomas; a prescrição
/// tem de pertencer ao plano aberto, que tem de estar ativo.
/// </summary>
internal sealed class MyPlanSupplementIntakeStore : IMyPlanSupplementIntakeStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;

    public MyPlanSupplementIntakeStore(
        PtManagerDbContext dbContext,
        ITrainerTimeZoneProvider timeZoneProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
    }

    public Task<MyPlanSupplementIntakeResult> MarkAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid itemId,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var target = await LoadTargetAsync(trainerId, clientUserId, itemId, now, cancellationToken);
            if (target.Status is not null)
                return new MyPlanSupplementIntakeResult(target.Status.Value, null);

            var existing = await FindIntakeAsync(target, itemId, cancellationToken);
            if (existing is not null)
                return new MyPlanSupplementIntakeResult(
                    MyPlanSupplementIntakeStatus.AlreadyMarked,
                    new MyPlanSupplementIntakeDto(itemId, existing.LocalDate, existing.TakenAt));
            if (target.IsUnavailable)
                return new MyPlanSupplementIntakeResult(MyPlanSupplementIntakeStatus.ItemUnavailable, null);

            var intake = new ClientMealPlanSupplementIntake(
                trainerId,
                target.ClientId,
                target.MealPlanId,
                target.IsMealSupplement ? itemId : null,
                target.IsMealSupplement ? null : itemId,
                target.SupplementId,
                target.LocalToday,
                now);
            _dbContext.ClientMealPlanSupplementIntakes.Add(intake);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new MyPlanSupplementIntakeResult(
                MyPlanSupplementIntakeStatus.Marked,
                new MyPlanSupplementIntakeDto(itemId, intake.LocalDate, intake.TakenAt));
        }, cancellationToken);

    public Task<MyPlanSupplementIntakeResult> UnmarkAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid itemId,
        DateTime now,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var target = await LoadTargetAsync(trainerId, clientUserId, itemId, now, cancellationToken);
            if (target.Status is not null)
                return new MyPlanSupplementIntakeResult(target.Status.Value, null);

            var existing = await FindIntakeAsync(target, itemId, cancellationToken);
            if (existing is null)
                return new MyPlanSupplementIntakeResult(MyPlanSupplementIntakeStatus.NotMarked, null);

            _dbContext.ClientMealPlanSupplementIntakes.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new MyPlanSupplementIntakeResult(MyPlanSupplementIntakeStatus.Unmarked, null);
        }, cancellationToken);

    private async Task<Target> LoadTargetAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid itemId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var client = await _dbContext.Clients
            .FromSqlInterpolated($"""
                SELECT *
                FROM clients
                WHERE owner_trainer_id = {trainerId}
                    AND user_id = {clientUserId}
                    AND is_active = true
                    AND is_deleted = false
                FOR UPDATE
            """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (client is null)
            return Target.Failed(MyPlanSupplementIntakeStatus.ProfileNotAvailable);

        var planId = await _dbContext.MealPlans
            .AsNoTracking()
            .Where(plan =>
                plan.Id == client.SelectedMealPlanId &&
                plan.OwnerTrainerId == trainerId &&
                plan.ClientId == client.Id &&
                plan.IsActive &&
                !plan.IsArchived)
            .Select(plan => (Guid?)plan.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!planId.HasValue)
            return Target.Failed(MyPlanSupplementIntakeStatus.ItemNotFound);

        var inMeal = await (
                from link in _dbContext.MealPlanMealSupplements.AsNoTracking()
                join meal in _dbContext.MealPlanMeals.AsNoTracking() on link.MealPlanMealId equals meal.Id
                join supplement in _dbContext.Supplements.AsNoTracking() on link.SupplementId equals supplement.Id
                // Bloqueado: não aparece no plano do cliente, logo não se marca nem desmarca.
                where link.Id == itemId && meal.MealPlanId == planId.Value &&
                    supplement.PlatformEnforcementStatus != Domain.ValueObjects.PlatformEnforcementStatus.Blocked
                select new { link.SupplementId, supplement.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        var outsideMeal = inMeal is not null
            ? null
            : await (
                    from item in _dbContext.MealPlanSupplements.AsNoTracking()
                    join supplement in _dbContext.Supplements.AsNoTracking() on item.SupplementId equals supplement.Id
                    where item.Id == itemId && item.MealPlanId == planId.Value &&
                        supplement.PlatformEnforcementStatus != Domain.ValueObjects.PlatformEnforcementStatus.Blocked
                    select new { item.SupplementId, supplement.IsActive })
                .SingleOrDefaultAsync(cancellationToken);

        var found = inMeal ?? outsideMeal;
        if (found is null)
            return Target.Failed(MyPlanSupplementIntakeStatus.ItemNotFound);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(trainerId, cancellationToken);
        return new Target(
            null,
            client.Id,
            planId.Value,
            found.SupplementId,
            IsMealSupplement: inMeal is not null,
            IsUnavailable: !found.IsActive,
            LocalDates.Today(now, timeZone));
    }

    private Task<ClientMealPlanSupplementIntake?> FindIntakeAsync(
        Target target,
        Guid itemId,
        CancellationToken cancellationToken) =>
        _dbContext.ClientMealPlanSupplementIntakes
            .Where(intake =>
                intake.ClientId == target.ClientId &&
                intake.LocalDate == target.LocalToday &&
                (target.IsMealSupplement
                    ? intake.MealPlanMealSupplementId == itemId
                    : intake.MealPlanSupplementId == itemId))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<MyPlanSupplementIntakeResult> ExecuteAsync(
        Func<Task<MyPlanSupplementIntakeResult>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var result = await operation();
            if (result.Status is MyPlanSupplementIntakeStatus.Marked or MyPlanSupplementIntakeStatus.Unmarked)
                await transaction.CommitAsync(cancellationToken);
            else
                await transaction.RollbackAsync(cancellationToken);

            return result;
        });
    }

    private sealed record Target(
        MyPlanSupplementIntakeStatus? Status,
        Guid ClientId,
        Guid MealPlanId,
        Guid SupplementId,
        bool IsMealSupplement,
        bool IsUnavailable,
        DateOnly LocalToday)
    {
        public static Target Failed(MyPlanSupplementIntakeStatus status) =>
            new(status, Guid.Empty, Guid.Empty, Guid.Empty, false, false, default);
    }
}
