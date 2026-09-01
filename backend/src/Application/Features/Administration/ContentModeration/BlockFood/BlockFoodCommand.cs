namespace Application.Features.Administration.ContentModeration.BlockFood;

/// <summary>Solicita o bloqueio de um alimento privado com motivo estruturado.</summary>
public sealed record BlockFoodCommand(
    Guid FoodId,
    string ReasonCode);
