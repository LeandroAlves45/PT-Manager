using Application.Features.Administration.Overview;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;

namespace Infrastructure.Persistence.Administration;

/// <summary>
/// Contagens da visão geral do superuser: cinco agregações, uma por catálogo. Ignora os
/// Global Query Filters por ser uma leitura administrativa e devolve apenas números.
/// </summary>
internal sealed class AdminOverviewQueries : IAdminOverviewQueries
{
    private readonly PtManagerDbContext _dbContext;

    public AdminOverviewQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<AdminOverviewDto> GetAsync(CancellationToken cancellationToken)
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

        return new AdminOverviewDto(
            globalFoods ?? EmptyCatalog,
            globalExercises ?? EmptyCatalog,
            globalSupplements ?? EmptyCatalog,
            privateFoods ?? EmptyModeration,
            privateExercises ?? EmptyModeration);
    }

    private static AdminOverviewDto.CatalogCountsDto EmptyCatalog => new(0, 0, 0);
    private static AdminOverviewDto.ModerationCountsDto EmptyModeration => new(0, 0);
}
