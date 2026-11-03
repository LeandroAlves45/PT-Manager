using Application.Features.Administration.ContentModeration.Dtos;
namespace Api.Contracts.Administration;

/// <summary>
/// Linha da fila de moderação. Do personal trainer dono expõe id e nome; o nome pode ser null
/// em contas legadas sem FullName e o email nunca é usado como fallback. <c>video_url</c> é a
/// ligação externa de um exercício (YouTube ou Vimeo) que os clientes abrem; null nos restantes.
/// </summary>
public sealed record ModerationQueueItemResponse(
    Guid Id,
    string Name,
    Guid OwnerTrainerId,
    string? OwnerTrainerName,
    bool IsActive,
    string PlatformEnforcementStatus,
    string? PlatformEnforcementReason,
    string? PlatformEnforcementNote,
    DateTime? PlatformEnforcedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? VideoUrl)
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
            dto.PlatformEnforcementNote,
            dto.PlatformEnforcedAt,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.VideoUrl);
    }
}
