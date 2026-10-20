namespace Application.Features.Administration.Trainers.Dtos;

/// <summary>
/// Ficha de um treinador vista pelo superuser. Só o número de clientes ativos: o superuser não
/// vê fichas, planos nem sessões de clientes (RN-018).
/// </summary>
public sealed record AdminTrainerDto(
    Guid TrainerId,
    string? FullName,
    string Email,
    string SubscriptionStatus,
    string SubscriptionTier,
    bool IsExemptFromBilling,
    DateTime? BillingExemptSince,
    bool HasPaidSubscription,
    int ActiveClientCount,
    DateTime CreatedAt);
