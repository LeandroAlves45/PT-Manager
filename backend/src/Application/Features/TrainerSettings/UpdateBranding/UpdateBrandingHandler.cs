using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.TrainerSettings.Abstractions;
using Application.Features.TrainerSettings.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.TrainerSettings.UpdateBranding;

/// <summary>Atualiza o branding visual do personal trainer autenticado.</summary>
public sealed class UpdateBrandingHandler
{
    private readonly IValidator<UpdateBrandingCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerSettingsStore _store;

    public UpdateBrandingHandler(
        IValidator<UpdateBrandingCommand> validator,
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
        UpdateBrandingCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<TrainerSettingsDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext, TrainerSettingsErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<TrainerSettingsDto>.Failure(actor.Error!);

        var outcome = await _store.UpdateBrandingAsync(
            actor.Value.TrainerId,
            command.AppName,
            command.PrimaryColor,
            command.BodyColor,
            _clock.UtcNow,
            cancellationToken);

        return Result<TrainerSettingsDto>.Success(outcome.Settings!.ToDto());
    }
}
