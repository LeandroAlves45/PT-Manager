using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Media;
using Application.Features.TrainerSettings.Abstractions;
using Application.Features.TrainerSettings.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.TrainerSettings.ReplaceLogo;

/// <summary>
/// Substitui o logo do personal trainer.
/// </summary>
/// <remarks>
/// <para>
/// A preparação e a publicação do asset ocorrem inteiramente antes de qualquer
/// transação PostgreSQL: nenhum efeito remoto mantém uma transação aberta.
/// </para>
/// </remarks>
public sealed class ReplaceLogoHandler
{
    private const string ErrorCodePrefix = "trainer_settings_logo";

    private readonly IValidator<ReplaceLogoCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly MediaPreparationPipeline _pipeline;
    private readonly IMediaStorage _mediaStorage;
    private readonly ITrainerSettingsStore _store;

    public ReplaceLogoHandler(
        IValidator<ReplaceLogoCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        MediaPreparationPipeline pipeline,
        IMediaStorage mediaStorage,
        ITrainerSettingsStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _mediaStorage = mediaStorage ?? throw new ArgumentNullException(nameof(mediaStorage));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<TrainerSettingsDto>> HandleAsync(
        ReplaceLogoCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<TrainerSettingsDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext, TrainerSettingsErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<TrainerSettingsDto>.Failure(actor.Error!);

        var prepared = await _pipeline.PrepareAsync(
            command.Logo,
            ImageProfiles.TrainerLogo,
            MediaAssetKind.TrainerLogo,
            actor.Value.TrainerId,
            cancellationToken);

        if (prepared.Status != MediaPreparationStatus.Prepared)
            return Result<TrainerSettingsDto>.Failure(
                MediaPreparationErrorMapper.ToError(prepared, "Logo", ErrorCodePrefix));

        var uploaded = prepared.Media!;
        var correlationId = Guid.NewGuid();

        try
        {
            var outcome = await _store.ReplaceLogoAsync(
                actor.Value.TrainerId,
                uploaded.Url,
                uploaded.PublicId,
                correlationId,
                _clock.UtcNow,
                cancellationToken);

            return Result<TrainerSettingsDto>.Success(outcome.Settings!.ToDto());
        }
        catch (OperationCanceledException)
        {
            if (!await TryDeleteUploadedMediaAsync(uploaded.PublicId, actor.Value.TrainerId))
                return Result<TrainerSettingsDto>.Failure(
                    TrainerSettingsErrors.LogoCompensationFailed);

            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!await TryDeleteUploadedMediaAsync(uploaded.PublicId, actor.Value.TrainerId))
                return Result<TrainerSettingsDto>.Failure(
                    TrainerSettingsErrors.LogoCompensationFailed);

            return Result<TrainerSettingsDto>.Failure(
                TrainerSettingsErrors.PersistenceFailed);
        }
    }

    /// <summary>
    /// Compensa o upload não referenciado.
    /// </summary>
    private async Task<bool> TryDeleteUploadedMediaAsync(string publicId, Guid trainerId)
    {
        try
        {
            var deletion = await _mediaStorage.DeleteAsync(
                publicId, trainerId, CancellationToken.None);

            return deletion.Status == MediaStorageStatus.Success;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
