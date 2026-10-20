using Application.Features.Administration.Trainers.Dtos;
using Application.Pagination;

namespace Application.Features.Administration.Trainers.Abstractions;

/// <summary>Leituras do superuser sobre treinadores, sem dados de clientes.</summary>
public interface ITrainerAdministrationQueries
{
    Task<TrainerLookupDto> LookupByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<AdminTrainerDto?> GetTrainerAsync(Guid trainerId, CancellationToken cancellationToken);

    Task<PageResult<AdminTrainerDto>> ListExemptAsync(PageRequest page, CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingTrainerInvitationDto>> ListPendingInvitationsAsync(
        CancellationToken cancellationToken);

    /// <summary>Subscrição Stripe paga que ainda pode cobrar, a cancelar antes de isentar.</summary>
    Task<string?> FindChargeableStripeSubscriptionAsync(Guid trainerId, CancellationToken cancellationToken);
}
