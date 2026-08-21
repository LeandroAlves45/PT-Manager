using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Packs.ClientSessionPacks.UpdateClientSessionPackExpectedEndDate;

/// <summary>Atualiza a expectativa temporal sem afetar o saldo.</summary>
public sealed class UpdateClientSessionPackExpectedEndDateHandler
{
    private readonly IValidator<UpdateClientSessionPackExpectedEndDateCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientSessionPackStore _store;

    public UpdateClientSessionPackExpectedEndDateHandler(
        IValidator<UpdateClientSessionPackExpectedEndDateCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IClientSessionPackStore store
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<ClientSessionPackDto>> HandleAsync(
        UpdateClientSessionPackExpectedEndDateCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ClientSessionPackDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, PackErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<ClientSessionPackDto>.Failure(actor.Error!);

        var outcome = await _store.UpdateExpectedEndDateAsync(
            actor.Value.TrainerId,
            command.ClientSessionPackId,
            command.ExpectedEndDate,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToUpdateResult();
    }
}
