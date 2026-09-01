namespace Application.Features.Administration.ContentModeration.UnblockFood;

/// <summary>Solicita a remoção do bloqueio administrativo de um alimento privado.</summary>
public sealed record UnblockFoodCommand(Guid FoodId);
