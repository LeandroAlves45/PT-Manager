using Domain.Entities.Nutrition;

namespace Application.Features.Nutrition.Foods.ListFoods;

/// <summary>Lista alimentos globais ativos e privados segundo o filtro.</summary>
public sealed record ListFoodsQuery(
    string? Search,
    FoodActivityFilter Activity = FoodActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50
);
