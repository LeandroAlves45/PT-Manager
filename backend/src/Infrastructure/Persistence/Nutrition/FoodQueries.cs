using System.Linq.Expressions;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Features.Nutrition.Foods.ListFoods;
using Application.Pagination;
using Domain.Entities.Nutrition;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Nutrition;

/// <summary>Executa queries paginadas de Food sem tracking.</summary>
internal sealed class FoodQueries : IFoodQueries
{
    private readonly PtManagerDbContext _dbContext;

    public FoodQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<FoodDto?> GetAsync(Guid foodId, CancellationToken cancellationToken) =>
        BaseVisibleQuery()
            .Where(food => food.Id == foodId)
            .Select(FoodProjection)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<FoodDto>> ListAsync(
        string? search,
        FoodActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var query = activity switch
        {
            FoodActivityFilter.Active => BaseVisibleQuery()
                .Where(food => food.OwnerTrainerId == null || food.IsActive),
            FoodActivityFilter.Archived => BaseVisibleQuery()
                .Where(food => food.OwnerTrainerId != null && !food.IsActive),
            FoodActivityFilter.All => BaseVisibleQuery(),
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikeSearchPattern.Build(search);
            query = query.Where(food =>
                EF.Functions.ILike(
                    food.Name, pattern, LikeSearchPattern.LikeEscapeCharacter)
                || food.Description != null
                    && EF.Functions.ILike(
                        food.Description, pattern, LikeSearchPattern.LikeEscapeCharacter)
            );
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(food => food.Name)
            .ThenBy(food => food.OwnerTrainerId == null)
            .ThenBy(food => food.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(FoodProjection)
            .ToListAsync(cancellationToken);

        return new PageResult<FoodDto>(items, totalCount);
    }

    // Visível: qualquer food privado do tenant (ativo ou arquivado) OU qualquer food global ativo.
    // Global inativo nunca é visível; privado arquivado continua visível para Get/List(Archived).
    private IQueryable<Food> BaseVisibleQuery() => _dbContext.Foods
        .AsNoTracking()
        .Where(food => food.OwnerTrainerId != null || food.IsActive);

    private static Expression<Func<Food, FoodDto>> FoodProjection => food => new FoodDto(
        food.Id,
        food.OwnerTrainerId == null ? "global" : "private",
        food.Name,
        food.Description,
        food.Protein,
        food.Carbs,
        food.Fats,
        food.Kcal,
        food.Fiber,
        food.IsActive,
        food.CreatedAt,
        food.UpdatedAt
    );
}
