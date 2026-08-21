using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.TrainerSettings.Abstractions;
using Application.Features.TrainerSettings.Dtos;
using Application.Results;

namespace Application.Features.TrainerSettings.ResetBrandingColors;

/// <summary>Repõe PrimaryColor e BodyColor no tema padrão(null). Idempotente.</summary>
public sealed class ResetBrandingColorsHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerSettingsStore _store;

    public ResetBrandingColorsHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerSettingsStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<TrainerSettingsDto>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext, TrainerSettingsErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<TrainerSettingsDto>.Failure(actor.Error!);

        var outcome = await _store.ResetBrandingColorsAsync(
            actor.Value.TrainerId,
            _clock.UtcNow,
            cancellationToken);

        return Result<TrainerSettingsDto>.Success(outcome.Settings!.ToDto());
    }
}
