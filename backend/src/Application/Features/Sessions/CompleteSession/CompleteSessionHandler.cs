using Application.Common.Abstractions;
using Application.Features.Sessions.Abstractions;
using Application.Features.Sessions.Dtos;
using Application.Results;

namespace Application.Features.Sessions.CompleteSession;

/// <summary>Conclui uma sessão a partir do seu instante de início.</summary>
public sealed class CompleteSessionHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ISessionStore _store;

    public CompleteSessionHandler(
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
        CompleteSessionCommand command,
        CancellationToken cancellationToken)
    {
        var tenant = SessionActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<SessionDto>.Failure(tenant.Error!);

        var outcome = await _store.TransitionAsync(
            tenant.Value,
            command.SessionId,
            SessionTransition.Complete,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToResult();
    }
}
