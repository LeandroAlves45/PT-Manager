using Application.Common.Time;
using Application.Features.Administration.Overview;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Administration;

/// <summary>
/// Contagens da visão geral do superuser: uma agregação por catálogo e uma por treinadores. Ignora os
/// Global Query Filters por ser uma leitura administrativa e devolve apenas números.
/// </summary>
internal sealed class AdminOverviewQueries : IAdminOverviewQueries
{
    private readonly PtManagerDbContext _dbContext;

    public AdminOverviewQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<AdminOverviewDto> GetAsync(
        UtcRange revenuePeriod,
        CancellationToken cancellationToken)
    {
        var globalFoods = await _dbContext.Foods
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(food => food.OwnerTrainerId == null)
            .GroupBy(_ => 1)
            .Select(group => new AdminOverviewDto.CatalogCountsDto(
                group.Count(),
                group.Count(food => food.IsActive),
                group.Count(food => !food.IsActive)))
            .FirstOrDefaultAsync(cancellationToken);

        var globalExercises = await _dbContext.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(exercise => exercise.OwnerTrainerId == null)
            .GroupBy(_ => 1)
            .Select(group => new AdminOverviewDto.CatalogCountsDto(
                group.Count(),
                group.Count(exercise => exercise.IsActive),
                group.Count(exercise => !exercise.IsActive)))
            .FirstOrDefaultAsync(cancellationToken);

        var globalSupplements = await _dbContext.Supplements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(supplement => supplement.OwnerTrainerId == null)
            .GroupBy(_ => 1)
            .Select(group => new AdminOverviewDto.CatalogCountsDto(
                group.Count(),
                group.Count(supplement => supplement.IsActive),
                group.Count(supplement => !supplement.IsActive)))
            .FirstOrDefaultAsync(cancellationToken);

        var privateFoods = await _dbContext.Foods
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(food => food.OwnerTrainerId != null)
            .GroupBy(_ => 1)
            .Select(group => new AdminOverviewDto.ModerationCountsDto(
                group.Count(),
                group.Count(food =>
                    food.PlatformEnforcementStatus == PlatformEnforcementStatus.Blocked)))
            .FirstOrDefaultAsync(cancellationToken);

        var privateExercises = await _dbContext.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(exercise => exercise.OwnerTrainerId != null)
            .GroupBy(_ => 1)
            .Select(group => new AdminOverviewDto.ModerationCountsDto(
                group.Count(),
                group.Count(exercise =>
                    exercise.PlatformEnforcementStatus == PlatformEnforcementStatus.Blocked)))
            .FirstOrDefaultAsync(cancellationToken);

        var privateSupplements = await _dbContext.Supplements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(supplement => supplement.OwnerTrainerId != null)
            .GroupBy(_ => 1)
            .Select(group => new AdminOverviewDto.ModerationCountsDto(
                group.Count(),
                group.Count(supplement =>
                    supplement.PlatformEnforcementStatus == PlatformEnforcementStatus.Blocked)))
            .FirstOrDefaultAsync(cancellationToken);

        // Só contam treinadores com conta utilizável; o isento é um número à parte e nunca
        // entra na subscrição ativa paga nem na suspensa (RN-018).
        var subscriptions = _dbContext.TrainerSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(subscription => _dbContext.Users
                .IgnoreQueryFilters()
                .Any(user => user.Id == subscription.TrainerId &&
                    user.Role == "trainer" && user.IsActive && !user.IsDeleted));

        var trainerCounts = await subscriptions
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Active = group.Count(subscription => !subscription.IsExemptFromBilling &&
                    subscription.Status == SubscriptionStatus.Active),
                Suspended = group.Count(subscription => !subscription.IsExemptFromBilling &&
                    subscription.Status == SubscriptionStatus.Suspended),
                Exempt = group.Count(subscription => subscription.IsExemptFromBilling),
                Blocked = group.Count(subscription => _dbContext.TrainerAccessBlocks
                    .Any(block => block.TrainerId == subscription.TrainerId))
            })
            .FirstOrDefaultAsync(cancellationToken);

        var periodStart = revenuePeriod.StartUtc.UtcDateTime;
        var periodEnd = revenuePeriod.EndUtc.UtcDateTime;
        var revenueCents = await _dbContext.PlatformSubscriptionPayments
            .AsNoTracking()
            .Where(payment => payment.Currency == "eur" &&
                payment.PaidAt >= periodStart && payment.PaidAt < periodEnd)
            .SumAsync(payment => (long?)payment.NetCents, cancellationToken) ?? 0;

        return new AdminOverviewDto(
            globalFoods ?? EmptyCatalog,
            globalExercises ?? EmptyCatalog,
            globalSupplements ?? EmptyCatalog,
            privateFoods ?? EmptyModeration,
            privateExercises ?? EmptyModeration,
            privateSupplements ?? EmptyModeration,
            new AdminOverviewDto.TrainerCountsDto(
                trainerCounts?.Active ?? 0,
                trainerCounts?.Suspended ?? 0,
                trainerCounts?.Exempt ?? 0,
                trainerCounts?.Blocked ?? 0,
                revenueCents));
    }

    private static AdminOverviewDto.CatalogCountsDto EmptyCatalog => new(0, 0, 0);
    private static AdminOverviewDto.ModerationCountsDto EmptyModeration => new(0, 0);
}
