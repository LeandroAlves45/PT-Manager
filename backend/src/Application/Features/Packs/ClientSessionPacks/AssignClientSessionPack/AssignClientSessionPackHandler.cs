using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Clients;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Packs.ClientSessionPacks.AssignClientSessionPack;

/// <summary>Atribui um PackType ativo a um cliente do tenant.</summary>
public sealed class AssignClientSessionPackHandler
{
    private readonly IValidator<AssignClientSessionPackCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly IClock _clock;
    private readonly IClientSessionPackStore _store;

    public AssignClientSessionPackHandler(
        IValidator<AssignClientSessionPackCommand> validator,
        ITenantContext tenantContext,
        ITrainerTimeZoneProvider timeZoneProvider,
        IClock clock,
        IClientSessionPackStore store
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<ClientSessionPackDto>> HandleAsync(
        AssignClientSessionPackCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ClientSessionPackDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, PackErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<ClientSessionPackDto>.Failure(actor.Error!);

        var now = _clock.UtcNow;
        var timezone = await _timeZoneProvider.GetRequiredAsync(
            actor.Value.TrainerId,
            cancellationToken
        );
        var localToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(now, timezone)
        );

        if (command.PurchaseDate > localToday)
            return Result<ClientSessionPackDto>.Failure(PackErrors.PurchaseDateInFuture());

        var outcome = await _store.AssignAsync(
            actor.Value.TrainerId,
            command.ClientId,
            command.PackTypeId,
            command.PurchaseDate,
            command.ExpectedEndDate,
            now,
            cancellationToken
        );

        return outcome.ToAssignResult();
    }
}
