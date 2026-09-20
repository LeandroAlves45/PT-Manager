namespace Application.Features.Administration.ContentModeration.Dtos;

/// <summary>
/// Linha da fila de moderação. Expõe o id e o nome do personal trainer dono,
/// o email nunca é devolvido, para manter a minimização de dados pessoais.
/// </summary>
public sealed record ModerationQueueItemDto(
    Guid Id,
    string Name,
    Guid OwnerTrainerId,
    string? OwnerTrainerName,
    bool IsActive,
    string PlatformEnforcementStatus,
    string? PlatformEnforcementReason,
    DateTime? PlatformEnforcedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
