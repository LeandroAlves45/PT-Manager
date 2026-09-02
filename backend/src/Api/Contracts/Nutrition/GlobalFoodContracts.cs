using Application.Features.Nutrition.Foods.Dtos;

namespace Api.Contracts.Nutrition;

/// <summary>Valores nutricionais por 100g de um alimento global novo.</summary>
public sealed record CreateGlobalFoodRequest(
    string Name,
    string? Description,
    decimal Protein,
    decimal Carbs,
    decimal Fats,
    decimal? Fiber);

/// <summary>Substitui os campos editáveis de um alimento global existente.</summary>
public sealed record UpdateGlobalFoodRequest(
    string Name,
    string? Description,
    decimal Protein,
    decimal Carbs,
    decimal Fats,
    decimal? Fiber);

/// <summary>Alimento global apresentado ao superuser.</summary>
public sealed record GlobalFoodResponse(
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
    DateTime UpdatedAt)
{
    /// <summary>Projeta o DTO da Application no contrato da API.</summary>
    public static GlobalFoodResponse From(GlobalFoodDto food)
    {
        ArgumentNullException.ThrowIfNull(food);

        return new(
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
            food.UpdatedAt
        );
    }
}
