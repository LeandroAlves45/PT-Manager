namespace Application.Features.Nutrition.Foods.GetFood;

/// <summary>Solicita um alimento global ativo ou privado do tenant.</summary>
public sealed record GetFoodQuery(Guid FoodId);
