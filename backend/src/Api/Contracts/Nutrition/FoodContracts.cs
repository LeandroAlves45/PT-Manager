using Application.Features.Nutrition.Foods.Dtos;

namespace Api.Contracts.Nutrition;

/// <summary>Valores nutricionais por 100g de um alimento privado novo.</summary>
public sealed record CreateFoodRequest(
    string Name,
    string? Description,
    decimal Protein,
    decimal Carbs,
    decimal Fats,
    decimal? Fiber);

/// <summary>Substitui os campos editáveis de um alimento privado existente.</summary>
public sealed record UpdateFoodRequest(
    string Name,
    string? Description,
    decimal Protein,
    decimal Carbs,
    decimal Fats,
    decimal? Fiber);

/// <summary>Alimento visível ao personal trainer, global ou privado.</summary>
public sealed record FoodResponse(
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
    DateTime UpdatedAt)
{
    /// <summary>Projeta o DTO da Application no contrato da API.</summary>
    public static FoodResponse From(FoodDto food)
    {
        ArgumentNullException.ThrowIfNull(food);

        return new(
            food.Id,
            food.Scope,
            food.Name,
            food.Description,
            food.Protein,
            food.Carbs,
            food.Fats,
            food.Kcal,
            food.Fiber,
            food.IsActive,
            food.PlatformEnforcementStatus,
            food.PlatformEnforcementReason,
            food.CreatedAt,
            food.UpdatedAt
        );
    }
}
