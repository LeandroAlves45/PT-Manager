using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Administration.Trainers.Abstractions;
using Application.Results;

namespace Application.Features.Administration.Trainers.RevokeTrainerInvitation;

public sealed record RevokeTrainerInvitationCommand(Guid InvitationId);

/// <summary>Anula um convite pendente sem enviar outro (U51). Repetir é seguro.</summary>
public sealed class RevokeTrainerInvitationHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly ITrainerAdministrationStore _store;
    private readonly IClock _clock;

    public RevokeTrainerInvitationHandler(
        ITenantContext tenantContext,
        ITrainerAdministrationStore store,
        IClock clock)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result> HandleAsync(
        RevokeTrainerInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            TrainerAdministrationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var status = await _store.RevokeInvitationAsync(
            actor.Value.UserId,
            command.InvitationId,
            _clock.UtcNow,
            cancellationToken);

        return status switch
        {
            TrainerAdministrationStoreStatus.Changed or
            TrainerAdministrationStoreStatus.Unchanged => Result.Success(),
            TrainerAdministrationStoreStatus.NotFound =>
                Result.Failure(TrainerAdministrationErrors.InvitationNotFound),
            TrainerAdministrationStoreStatus.ActorInvalid =>
                Result.Failure(TrainerAdministrationErrors.AdministratorOnly),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
    }
}
