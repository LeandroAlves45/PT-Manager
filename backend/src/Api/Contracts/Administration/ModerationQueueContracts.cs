using Application.Features.Administration.ContentModeration.Dtos;
namespace Api.Contracts.Administration;

/// <summary>
/// Linha da fila de moderação. Do personal trainer dono expõe id e nome; o nome pode ser null
/// em contas legadas sem FullName e o email nunca é usado como fallback.
/// </summary>
public sealed record ModerationQueueItemResponse(
    Guid Id,
    string Name,
    Guid OwnerTrainerId,
    string? OwnerTrainerName,
    bool IsActive,
    string PlatformEnforcementStatus,
    string? PlatformEnforcementReason,
    DateTime? PlatformEnforcedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ModerationQueueItemResponse From(ModerationQueueItemDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new ModerationQueueItemResponse(
            dto.Id,
            dto.Name,
            dto.OwnerTrainerId,
            dto.OwnerTrainerName,
            dto.IsActive,
            dto.PlatformEnforcementStatus,
            dto.PlatformEnforcementReason,
            dto.PlatformEnforcedAt,
            dto.CreatedAt,
            dto.UpdatedAt);
    }
}
