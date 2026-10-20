using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Administration.Trainers.Abstractions;
using Application.Features.Administration.Trainers.Dtos;
using Application.Results;

namespace Application.Features.Administration.Trainers.ListTrainerInvitations;

public sealed record ListTrainerInvitationsQuery;

/// <summary>Convites isentos ainda pendentes, incluindo os já expirados por reenviar.</summary>
public sealed class ListTrainerInvitationsHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly ITrainerAdministrationQueries _queries;

    public ListTrainerInvitationsHandler(
        ITenantContext tenantContext,
        ITrainerAdministrationQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<IReadOnlyList<PendingTrainerInvitationDto>>> HandleAsync(
        ListTrainerInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            TrainerAdministrationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<IReadOnlyList<PendingTrainerInvitationDto>>.Failure(actor.Error!);

        return Result<IReadOnlyList<PendingTrainerInvitationDto>>.Success(
            await _queries.ListPendingInvitationsAsync(cancellationToken));
    }
}
