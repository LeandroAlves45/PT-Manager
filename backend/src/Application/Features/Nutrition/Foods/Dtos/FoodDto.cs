namespace Application.Features.Nutrition.Foods.Dtos;

/// <summary>Representa um alimento global ou privado com valores por 100g.</summary>
public sealed record FoodDto(
    Guid Id,
    string Scope,
    string Name,
    string? Description,
    decimal Protein,
    decimal Carbs,
    decimal Fats,
    decimal Kcal,
    decimal? Fiber,
    bool IsActive,
    string PlatformEnforcementStatus,
    string? PlatformEnforcementReason,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
