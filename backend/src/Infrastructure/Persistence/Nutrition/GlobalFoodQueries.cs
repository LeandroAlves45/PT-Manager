using System.Linq.Expressions;
using Application.Features.Nutrition.Foods;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Features.Nutrition.Foods.ListGlobalFoods;
using Application.Pagination;
using Domain.Entities.Nutrition;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Nutrition;

/// <summary>Consulta exclusivamente alimentos globais para administração.</summary>
internal sealed class GlobalFoodQueries : IGlobalFoodQueries
{
    private readonly PtManagerDbContext _dbContext;

    public GlobalFoodQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<GlobalFoodDto?> GetAsync(
        Guid foodId,
        CancellationToken cancellationToken) =>
        BaseQuery()
            .Where(food => food.Id == foodId)
            .Select(Projection)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<GlobalFoodDto>> ListAsync(
        string? search,
        GlobalFoodActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = activity switch
        {
            GlobalFoodActivityFilter.Active => BaseQuery().Where(food => food.IsActive),
            GlobalFoodActivityFilter.Archived => BaseQuery().Where(food => !food.IsActive),
            GlobalFoodActivityFilter.All => BaseQuery(),
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikeSearchPattern.Build(search);
            query = query.Where(food =>
                EF.Functions.ILike(food.Name, pattern, LikeSearchPattern.LikeEscapeCharacter)
                || food.Description != null &&
                EF.Functions.ILike(food.Description, pattern, LikeSearchPattern.LikeEscapeCharacter));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(food => food.Name)
            .ThenBy(food => food.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PageResult<GlobalFoodDto>(items, totalCount);
    }

    private IQueryable<Food> BaseQuery() =>
        _dbContext.Foods
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(food => food.OwnerTrainerId == null);

    private static Expression<Func<Food, GlobalFoodDto>> Projection => food =>
        new GlobalFoodDto(
            food.Id,
            food.Name,
            food.Description,
            food.Protein,
            food.Carbs,
            food.Fats,
            food.Kcal,
            food.Fiber,
            food.IsActive,
            food.CreatedAt,
            food.UpdatedAt);
}
