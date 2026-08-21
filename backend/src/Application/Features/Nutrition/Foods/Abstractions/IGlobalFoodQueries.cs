using Application.Features.Nutrition.Foods.Dtos;
using Application.Features.Nutrition.Foods.ListGlobalFoods;
using Application.Pagination;

namespace Application.Features.Nutrition.Foods.Abstractions;

/// <summary>Consulta exclusivamente alimentos globais para administração.</summary>
public interface IGlobalFoodQueries
{
    Task<GlobalFoodDto?> GetAsync(Guid foodId, CancellationToken cancellationToken);

    Task<PageResult<GlobalFoodDto>> ListAsync(
        string? search,
        GlobalFoodActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
