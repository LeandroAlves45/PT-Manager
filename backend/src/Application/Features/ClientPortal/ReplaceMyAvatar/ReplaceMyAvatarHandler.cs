using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Media;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.ClientPortal.ReplaceMyAvatar;

/// <summary>Substitui a fotografia de perfil do cliente autenticado.</summary>
public sealed class ReplaceMyAvatarHandler
{
    private const string ErrorCodePrefix = "portal_avatar";

    private readonly IValidator<ReplaceMyAvatarCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly MediaPreparationPipeline _pipeline;
    private readonly IMediaStorage _mediaStorage;
    private readonly IMyAvatarStore _store;

    public ReplaceMyAvatarHandler(
        IValidator<ReplaceMyAvatarCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        MediaPreparationPipeline pipeline,
        IMediaStorage mediaStorage,
        IMyAvatarStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _mediaStorage = mediaStorage ?? throw new ArgumentNullException(nameof(mediaStorage));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<MyProfileDto>> HandleAsync(
        ReplaceMyAvatarCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<MyProfileDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireClient(
            _tenantContext,
            ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyProfileDto>.Failure(actor.Error!);

        var prepared = await _pipeline.PrepareAsync(
            command.Avatar,
            ImageProfiles.ClientAvatar,
            MediaAssetKind.ClientAvatar,
            actor.Value.TrainerId,
            cancellationToken);

        if (prepared.Status != MediaPreparationStatus.Prepared)
            return Result<MyProfileDto>.Failure(
                MediaPreparationErrorMapper.ToError(prepared, "Avatar", ErrorCodePrefix));

        var uploaded = prepared.Media!;
        var correlationId = Guid.NewGuid();

        try
        {
            var outcome = await _store.ReplaceAsync(
                actor.Value.TrainerId,
                actor.Value.UserId,
                uploaded.Url,
                uploaded.PublicId,
                correlationId,
                _clock.UtcNow,
                cancellationToken);

            if (outcome.Status == MyAvatarStatus.Updated)
                return Result<MyProfileDto>.Success(outcome.Profile!);

            // A ficha não existe: o asset acabado de publicar nunca será
            // referenciado, por isso é compensado como se a transação tivesse
            // falhado.
            if (!await TryDeleteUploadedMediaAsync(uploaded.PublicId, actor.Value.TrainerId))
                return Result<MyProfileDto>.Failure(ClientPortalErrors.AvatarCompensationFailed);
            return Result<MyProfileDto>.Failure(ClientPortalErrors.ProfileNotAvailable);
        }
        catch (OperationCanceledException)
        {
            if (!await TryDeleteUploadedMediaAsync(uploaded.PublicId, actor.Value.TrainerId))
                return Result<MyProfileDto>.Failure(
                    ClientPortalErrors.AvatarCompensationFailed);

            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!await TryDeleteUploadedMediaAsync(uploaded.PublicId, actor.Value.TrainerId))
                return Result<MyProfileDto>.Failure(
                    ClientPortalErrors.AvatarCompensationFailed);

            return Result<MyProfileDto>.Failure(ClientPortalErrors.AvatarPersistenceFailed);
        }
    }

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
