namespace Application.Features.Nutrition.Foods.ListGlobalFoods;

/// <summary>Pesquisa paginada do catálogo global de alimentos.</summary>
public sealed record ListGlobalFoodsQuery(
    string? Search,
    GlobalFoodActivityFilter Activity = GlobalFoodActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50
);
