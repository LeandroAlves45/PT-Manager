using Application.Features.Administration.Trainers.Dtos;

namespace Application.Features.Administration.Trainers.Abstractions;

/// <summary>
/// Escritas auditadas do superuser. O ator é revalidado dentro da transação e cada mutação
/// grava uma entrada de auditoria administrativa.
/// </summary>
public interface ITrainerAdministrationStore
{
    Task<TrainerAdministrationStoreStatus> SetBillingExemptionAsync(
        Guid actorUserId,
        Guid trainerId,
        bool isExempt,
        DateTime now,
        CancellationToken cancellationToken);

    Task<IssueTrainerInvitationStoreResult> IssueInvitationAsync(
        Guid actorUserId,
        string email,
        string tokenHash,
        DateTime now,
        CancellationToken cancellationToken);

    Task<TrainerAdministrationStoreStatus> RevokeInvitationAsync(
        Guid actorUserId,
        Guid invitationId,
        DateTime now,
        CancellationToken cancellationToken);
}

public enum TrainerAdministrationStoreStatus
{
    Changed,
    Unchanged,
    NotFound,
    EmailAlreadyRegistered,
    ActorInvalid,
    ConcurrencyConflict
}

public sealed record IssueTrainerInvitationStoreResult(
    TrainerAdministrationStoreStatus Status,
    PendingTrainerInvitationDto? Invitation);
