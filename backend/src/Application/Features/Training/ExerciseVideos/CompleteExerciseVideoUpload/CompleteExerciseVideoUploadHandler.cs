using Application.Common.Abstractions;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Application.Features.Training.ExerciseVideos.Dtos;
using Application.Results;
using Domain.ValueObjects;

namespace Application.Features.Training.ExerciseVideos.CompleteExerciseVideoUpload;

/// <summary>
/// Finaliza um upload direto depois de confirmar no fornecedor o objeto, o
/// tamanho real e o tipo, e agenda a validação técnica.
/// </summary>
/// <remarks>
/// <para>
/// A consulta ao fornecedor acontece sem transação aberta. O store volta a
/// confirmar estado e janela dentro da transação: uma finalização concorrente
/// ou tardia nunca passa um vídeo a Processing duas vezes.
/// </para>
/// </remarks>
public sealed class CompleteExerciseVideoUploadHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IVideoObjectStorage _storage;
    private readonly IExerciseVideoStore _store;

    public CompleteExerciseVideoUploadHandler(
        ITenantContext tenantContext,
        IClock clock,
        IVideoObjectStorage storage,
        IExerciseVideoStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<ExerciseVideoDto>> HandleAsync(
        CompleteExerciseVideoUploadCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ExerciseId == Guid.Empty)
            return Result<ExerciseVideoDto>.Failure(TrainingErrors.ExerciseIdRequired());

        if (command.VideoId == Guid.Empty)
            return Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.VideoIdRequired());

        var actor = ExerciseVideoActors.ResolveWriter(_tenantContext, command.Catalog);
        if (!actor.IsSuccess)
            return Result<ExerciseVideoDto>.Failure(actor.Error!);

        var video = await _store.FindUploadAsync(
            command.Catalog,
            command.ExerciseId,
            command.VideoId,
            actor.Value.OwnerTrainerId,
            cancellationToken);

        if (video is null)
            return Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.UploadNotFound);

        if (video.Status == ExerciseVideoStatus.Processing ||
            video.Status == ExerciseVideoStatus.Ready)
            return Result<ExerciseVideoDto>.Success(video.ToDto());

        if (video.Status != ExerciseVideoStatus.Pending)
            return Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.UploadClosed);

        if (!video.IsUploadWindowOpen(_clock.UtcNow))
            return Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.UploadExpired);

        var info = await _storage.GetObjectInfoAsync(
            video.ObjectKey,
            video.OwnerTrainerId,
            cancellationToken);

        if (info.Status == VideoStorageStatus.NotFound)
            return Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.UploadIncomplete);

        if (info.Status != VideoStorageStatus.Success || info.Info is null)
            return Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.StorageUnavailable);

        var completion = new ExerciseVideoUploadCompletion(
            command.Catalog,
            command.ExerciseId,
            command.VideoId,
            actor.Value.OwnerTrainerId,
            actor.Value.UserId,
            Guid.NewGuid(),
            _clock.UtcNow);

        var mismatch = FindMismatch(video.DeclaredSizeBytes, video.ContentType, info.Info);
        if (mismatch is not null)
        {
            var rejection = await _store.RejectUploadAsync(completion, mismatch, cancellationToken);
            return rejection.Status switch
            {
                ExerciseVideoUploadTransitionStatus.Applied or
                ExerciseVideoUploadTransitionStatus.AlreadyApplied =>
                    Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.UploadRejected),
                _ => MapTransitionFailure(rejection.Status)
            };
        }

        var transition = await _store.MarkUploadedAsync(
            completion,
            info.Info.SizeBytes,
            info.Info.ETag,
            cancellationToken);

        return transition.Status is ExerciseVideoUploadTransitionStatus.Applied or
            ExerciseVideoUploadTransitionStatus.AlreadyApplied
            ? Result<ExerciseVideoDto>.Success(transition.Video!.ToDto())
            : MapTransitionFailure(transition.Status);
    }

    /// <summary>
    /// O tamanho declarado é o assinado na autorização; o R2 não garante impor o
    /// Content-Length, pelo que esta comparação é a autoridade do tamanho.
    /// </summary>
    private static string? FindMismatch(
        long declaredSizeBytes,
        string declaredContentType,
        VideoObjectInfo info)
    {
        if (info.SizeBytes != declaredSizeBytes || info.SizeBytes > ExerciseVideoPolicy.MaxSizeBytes)
            return "exercise_video_size_mismatch";

        if (!ExerciseVideoPolicy.ContentTypesMatch(declaredContentType, info.ContentType))
            return "exercise_video_content_type_mismatch";

        return null;
    }

    private static Result<ExerciseVideoDto> MapTransitionFailure(
        ExerciseVideoUploadTransitionStatus status) =>
        status switch
        {
            ExerciseVideoUploadTransitionStatus.NotFound =>
                Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.UploadNotFound),
            ExerciseVideoUploadTransitionStatus.UploadWindowClosed =>
                Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.UploadExpired),
            ExerciseVideoUploadTransitionStatus.InvalidState =>
                Result<ExerciseVideoDto>.Failure(ExerciseVideoErrors.StateConflict),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
}
