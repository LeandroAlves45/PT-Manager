using Application.Features.Nutrition.Foods.Dtos;
using Application.Features.Nutrition.Foods.ListFoods;
using Application.Pagination;

namespace Application.Features.Nutrition.Foods.Abstractions;

/// <summary>Executa leituras projetadas de alimentos visíveis ao tenant.</summary>
public interface IFoodQueries
{
    Task<FoodDto?> GetAsync(Guid foodId, CancellationToken cancellationToken);

    Task<PageResult<FoodDto>> ListAsync(
        string? search,
        FoodActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
