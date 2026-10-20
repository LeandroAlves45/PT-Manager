namespace Application.Features.Administration.Trainers.Dtos;

/// <summary>Convite isento ainda por usar nem anular; pode já ter expirado.</summary>
public sealed record PendingTrainerInvitationDto(
    Guid InvitationId,
    string Email,
    DateTime SentAt,
    DateTime ExpiresAt);
