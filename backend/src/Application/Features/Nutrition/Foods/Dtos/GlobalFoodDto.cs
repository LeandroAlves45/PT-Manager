namespace Application.Features.Nutrition.Foods.Dtos;

/// <summary>Alimento global apresentado a um superuser autorizado.</summary>
public sealed record GlobalFoodDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Protein,
    decimal Carbs,
    decimal Fats,
    decimal Kcal,
    decimal? Fiber,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
