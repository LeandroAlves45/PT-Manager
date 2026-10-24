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
    DateTime CreatedAt,
    TrainerAccessBlockDto? AccessBlock);

/// <summary>
/// Bloqueio ativo da conta (RN-013): o motivo e a nota que o personal trainer vê, e até quando
/// os clientes continuam a entrar no portal.
/// </summary>
public sealed record TrainerAccessBlockDto(
    string Reason,
    string? Note,
    DateTime BlockedAt,
    DateTime ClientAccessEndsAt);
