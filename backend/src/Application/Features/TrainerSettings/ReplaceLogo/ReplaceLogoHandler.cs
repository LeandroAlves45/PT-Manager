using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.TrainerSettings.Abstractions;
using Application.Features.TrainerSettings.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.TrainerSettings.ReplaceLogo;

/// <summary>
/// Substitui o logo do personal trainer. O upload ocorre antes de qualquer transação
/// PostgreSQL; se a persistência falhar depois de um upload bem-sucedido, o
/// handler tenta eliminar imediatamente o novo asset para não deixar um
/// ficheiro órfão a acumular custos no storage externo.
/// </summary>
public sealed class ReplaceLogoHandler
{
    private readonly IValidator<ReplaceLogoCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMediaStorage _mediaStorage;
    private readonly ITrainerSettingsStore _store;

    public ReplaceLogoHandler(
        IValidator<ReplaceLogoCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IMediaStorage mediaStorage,
        ITrainerSettingsStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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

        StoredMedia uploaded;
        try
        {
            uploaded = await _mediaStorage.UploadAsync(
                command.Logo, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<TrainerSettingsDto>.Failure(
                TrainerSettingsErrors.MediaUploadFailed);
        }

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
            if (!await TryDeleteUploadedMediaAsync(uploaded.PublicId))
            {
                return Result<TrainerSettingsDto>.Failure(
                    TrainerSettingsErrors.LogoCompensationFailed);
            }

            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // O upload já está confirmado no storage externo; a transação
            // local falhou depois disso. Compensar de imediato para não
            // deixar o asset órfão — não há segunda oportunidade automática,
            // porque a outbox só é escrita DENTRO da transação que falhou.
            if (!await TryDeleteUploadedMediaAsync(uploaded.PublicId))
            {
                return Result<TrainerSettingsDto>.Failure(
                    TrainerSettingsErrors.LogoCompensationFailed);
            }

            return Result<TrainerSettingsDto>.Failure(
                TrainerSettingsErrors.PersistenceFailed);
        }
    }

    private async Task<bool> TryDeleteUploadedMediaAsync(string publicId)
    {
        try
        {
            await _mediaStorage.DeleteAsync(publicId, CancellationToken.None);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
