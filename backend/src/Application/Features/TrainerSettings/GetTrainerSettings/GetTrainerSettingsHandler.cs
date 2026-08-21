using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.TrainerSettings.Abstractions;
using Application.Features.TrainerSettings.Dtos;
using Application.Results;

namespace Application.Features.TrainerSettings.GetTrainerSettings;

/// <summary>Obtém as definições completas do personal trainer autenticado.</summary>
public sealed class GetTrainerSettingsHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly ITrainerSettingsQueries _queries;

    public GetTrainerSettingsHandler(
        ITenantContext tenantContext,
        ITrainerSettingsQueries queries
    )
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<TrainerSettingsDto>> HandleAsync(
        CancellationToken cancellationToken
    )
    {
        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext, TrainerSettingsErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<TrainerSettingsDto>.Failure(actor.Error!);

        var settings = await _queries.GetAsync(actor.Value.TrainerId, cancellationToken)
            ?? throw new InvalidOperationException(
                "TrainerSettings must exist for every personal trainer onboarding.");

        return Result<TrainerSettingsDto>.Success(settings);
    }
}
