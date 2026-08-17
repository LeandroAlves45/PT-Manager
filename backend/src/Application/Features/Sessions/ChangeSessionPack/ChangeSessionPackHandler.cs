using Application.Common.Abstractions;
using Application.Features.Sessions.Abstractions;
using Application.Features.Sessions.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Sessions.ChangeSessionPack;

/// <summary>Altera o pack de uma sessão ainda Scheduled.</summary>
public sealed class ChangeSessionPackHandler
{
    private readonly IValidator<ChangeSessionPackCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ISessionStore _store;

    public ChangeSessionPackHandler(
        IValidator<ChangeSessionPackCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        ISessionStore store)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);

        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _store = store;
    }

    public async Task<Result<SessionDto>> HandleAsync(
        ChangeSessionPackCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<SessionDto>.Failure(validation.ToApplicationError());

        var tenant = SessionActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<SessionDto>.Failure(tenant.Error!);

        var outcome = await _store.ChangePackAsync(
            tenant.Value,
            command.SessionId,
            command.ClientSessionPackId,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToResult();
    }
}
