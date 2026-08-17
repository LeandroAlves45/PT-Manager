using Application.Common.Abstractions;
using Application.Features.Sessions.Abstractions;
using Application.Features.Sessions.Dtos;
using Application.Results;

namespace Application.Features.Sessions.RestoreSession;

/// <summary>Restaura uma sessão pelo personal trainer sem alterar o seu agendamento.</summary>
public sealed class RestoreSessionHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ISessionStore _store;

    public RestoreSessionHandler(
        ITenantContext tenantContext,
        IClock clock,
        ISessionStore store)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);

        _tenantContext = tenantContext;
        _clock = clock;
        _store = store;
    }

    public async Task<Result<SessionDto>> HandleAsync(
        RestoreSessionCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenant = SessionActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<SessionDto>.Failure(tenant.Error!);

        var outcome = await _store.TransitionAsync(
            tenant.Value,
            command.SessionId,
            SessionTransition.Restore,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToResult();
    }
}
