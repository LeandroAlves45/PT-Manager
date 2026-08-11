namespace Application.Features.Nutrition.Foods.ArchiveFood;

/// <summary>Solicita a desativação idempotente de um alimento privado.</summary>
public sealed record ArchiveFoodCommand(Guid FoodId);
