using Application.Features.Administration.Trainers.Dtos;

namespace Api.Contracts.Administration;

/// <summary>Ficha do treinador vista pelo superuser; só o número de clientes ativos.</summary>
public sealed record AdminTrainerResponse(
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
    TrainerAccessBlockResponse? AccessBlock)
{
    public static AdminTrainerResponse From(AdminTrainerDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new AdminTrainerResponse(
            dto.TrainerId,
            dto.FullName,
            dto.Email,
            dto.SubscriptionStatus,
            dto.SubscriptionTier,
            dto.IsExemptFromBilling,
            dto.BillingExemptSince,
            dto.HasPaidSubscription,
            dto.ActiveClientCount,
            dto.CreatedAt,
            dto.AccessBlock is null ? null : TrainerAccessBlockResponse.From(dto.AccessBlock));
    }
}

/// <summary>
/// Bloqueio ativo da conta: o motivo e a nota que o personal trainer vê e até quando os
/// clientes continuam a entrar no portal.
/// </summary>
public sealed record TrainerAccessBlockResponse(
    string Reason,
    string? Note,
    DateTime BlockedAt,
    DateTime ClientAccessEndsAt)
{
    public static TrainerAccessBlockResponse From(TrainerAccessBlockDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new TrainerAccessBlockResponse(dto.Reason, dto.Note, dto.BlockedAt, dto.ClientAccessEndsAt);
    }
}

/// <summary>Um dos quatro motivos e uma nota livre opcional para bloquear a conta.</summary>
public sealed record BlockTrainerRequest(string ReasonCode, string? Note = null);

/// <summary>Convite isento pendente; pode já ter expirado.</summary>
public sealed record PendingTrainerInvitationResponse(
    Guid InvitationId,
    string Email,
    DateTime SentAt,
    DateTime ExpiresAt)
{
    public static PendingTrainerInvitationResponse From(PendingTrainerInvitationDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new PendingTrainerInvitationResponse(
            dto.InvitationId, dto.Email, dto.SentAt, dto.ExpiresAt);
    }
}

/// <summary>
/// Resultado da procura. <c>status</c> é <c>found</c>, <c>invite_pending</c>, <c>not_found</c>
/// ou <c>not_trainer</c>; só o primeiro traz a ficha e só o segundo traz o convite.
/// </summary>
public sealed record TrainerLookupResponse(
    string Status,
    AdminTrainerResponse? Trainer,
    PendingTrainerInvitationResponse? Invitation)
{
    public static TrainerLookupResponse From(TrainerLookupDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new TrainerLookupResponse(
            dto.Status switch
            {
                TrainerLookupStatus.Found => "found",
                TrainerLookupStatus.InvitePending => "invite_pending",
                TrainerLookupStatus.NotFound => "not_found",
                TrainerLookupStatus.NotTrainer => "not_trainer",
                _ => throw new ArgumentOutOfRangeException(nameof(dto))
            },
            dto.Trainer is null ? null : AdminTrainerResponse.From(dto.Trainer),
            dto.Invitation is null ? null : PendingTrainerInvitationResponse.From(dto.Invitation));
    }
}

public sealed record InviteTrainerRequest(string? Email);

public sealed record TrainerLookupRequest(string? Email);
