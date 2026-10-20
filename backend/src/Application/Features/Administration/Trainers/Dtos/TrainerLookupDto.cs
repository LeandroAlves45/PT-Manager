namespace Application.Features.Administration.Trainers.Dtos;

/// <summary>Resultado da procura pelo email completo; nunca sugere outros emails.</summary>
public sealed record TrainerLookupDto(
    TrainerLookupStatus Status,
    AdminTrainerDto? Trainer,
    PendingTrainerInvitationDto? Invitation);

public enum TrainerLookupStatus
{
    Found,
    InvitePending,
    NotFound,
    NotTrainer
}
