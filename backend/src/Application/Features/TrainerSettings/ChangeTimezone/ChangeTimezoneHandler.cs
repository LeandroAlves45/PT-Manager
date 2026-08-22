using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.TrainerSettings.Abstractions;
using Application.Features.TrainerSettings.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.TrainerSettings.ChangeTimezone;

/// <summary>
/// Altera o timezone do personal trainer autenticado, verificando conflitos
/// de agenda sob lock. Alterar o timezone atual é sucesso idempotente.
/// </summary>
public sealed class ChangeTimezoneHandler
{
    private readonly IValidator<ChangeTimezoneCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerSettingsStore _store;

    public ChangeTimezoneHandler(
        IValidator<ChangeTimezoneCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        ITrainerSettingsStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<TrainerSettingsDto>> HandleAsync(
        ChangeTimezoneCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<TrainerSettingsDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext, TrainerSettingsErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<TrainerSettingsDto>.Failure(actor.Error!);

        var outcome = await _store.ChangeTimezoneAsync(
            actor.Value.TrainerId,
            command.Timezone.Trim(),
            _clock.UtcNow,
            cancellationToken);

        return outcome.Kind switch
        {
            TrainerSettingsStoreResult.Status.Updated =>
                Result<TrainerSettingsDto>.Success(outcome.Settings!.ToDto()),
            TrainerSettingsStoreResult.Status.ScheduleConflict =>
                Result<TrainerSettingsDto>.Failure(TrainerSettingsErrors.ScheduleConflict),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
