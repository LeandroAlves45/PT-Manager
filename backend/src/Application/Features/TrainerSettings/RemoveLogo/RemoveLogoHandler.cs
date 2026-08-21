using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.TrainerSettings.Abstractions;
using Application.Features.TrainerSettings.Dtos;
using Application.Results;

namespace Application.Features.TrainerSettings.RemoveLogo;

/// <summary>
/// Remove o logo atual. Idempotente: sem logo, é um no-op de sucesso. Quando
/// existe logo, agenda a eliminação do asset via outbox — nunca chama
/// <see cref="IMediaStorage"/> diretamente, porque não há upload a compensar.
/// </summary>
public sealed class RemoveLogoHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerSettingsStore _store;

    public RemoveLogoHandler(
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

        var outcome = await _store.RemoveLogoAsync(
            actor.Value.TrainerId,
            Guid.NewGuid(),
            _clock.UtcNow,
            cancellationToken);

        return Result<TrainerSettingsDto>.Success(outcome.Settings!.ToDto());
    }
}
