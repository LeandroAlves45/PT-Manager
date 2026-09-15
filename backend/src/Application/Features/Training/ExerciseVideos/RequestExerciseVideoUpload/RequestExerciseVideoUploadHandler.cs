using Application.Common.Abstractions;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Application.Features.Training.ExerciseVideos.Dtos;
using Application.Results;
using Application.Validation;
using Domain.Entities.Training;
using FluentValidation;

namespace Application.Features.Training.ExerciseVideos.RequestExerciseVideoUpload;

/// <summary>
/// Regista um vídeo pendente e devolve a autorização de upload direto.
/// </summary>
public sealed class RequestExerciseVideoUploadHandler
{
    private readonly IValidator<RequestExerciseVideoUploadCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ExerciseVideoSettings _settings;
    private readonly IVideoObjectStorage _storage;
    private readonly IExerciseVideoStore _store;

    public RequestExerciseVideoUploadHandler(
        IValidator<RequestExerciseVideoUploadCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        ExerciseVideoSettings settings,
        IVideoObjectStorage storage,
        IExerciseVideoStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<ExerciseVideoUploadDto>> HandleAsync(
        RequestExerciseVideoUploadCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ExerciseVideoUploadDto>.Failure(validation.ToApplicationError());

        var actor = ExerciseVideoActors.ResolveWriter(_tenantContext, command.Catalog);
        if (!actor.IsSuccess)
            return Result<ExerciseVideoUploadDto>.Failure(actor.Error!);

        var now = _clock.UtcNow;
        var expiresAt = now.Add(_settings.UploadUrlLifetime);
        var video = new ExerciseVideo(
            command.ExerciseId,
            actor.Value.OwnerTrainerId,
            command.ContentType,
            command.SizeBytes,
            actor.Value.UserId,
            expiresAt,
            now);

        var authorization = await _storage.CreateUploadAuthorizationAsync(
            video.ObjectKey,
            video.OwnerTrainerId,
            video.ContentType,
            video.DeclaredSizeBytes,
            expiresAt,
            cancellationToken);

        if (authorization.Status != VideoStorageStatus.Success || authorization.Authorization is null)
            return Result<ExerciseVideoUploadDto>.Failure(ExerciseVideoErrors.StorageUnavailable);

        var registration = await _store.RegisterUploadAsync(
            new ExerciseVideoRegistration(
                command.Catalog,
                video,
                actor.Value.UserId,
                expiresAt.Add(_settings.AbandonmentGrace),
                _settings.MaxVideosPerTrainer,
                Guid.NewGuid(),
                now),
            cancellationToken);

        return registration switch
        {
            ExerciseVideoRegistrationStatus.Registered =>
                Result<ExerciseVideoUploadDto>.Success(new ExerciseVideoUploadDto(
                    video.ToDto(),
                    authorization.Authorization.Method,
                    authorization.Authorization.Url,
                    authorization.Authorization.ContentType,
                    authorization.Authorization.ExpiresAt,
                    ExerciseVideoPolicy.MaxSizeBytes)),
            ExerciseVideoRegistrationStatus.ExerciseNotFound =>
                Result<ExerciseVideoUploadDto>.Failure(TrainingErrors.ExerciseNotFound),
            ExerciseVideoRegistrationStatus.ExerciseInactive =>
                Result<ExerciseVideoUploadDto>.Failure(ExerciseVideoErrors.ExerciseInactive),
            ExerciseVideoRegistrationStatus.ExerciseBlocked =>
                Result<ExerciseVideoUploadDto>.Failure(ExerciseVideoErrors.ExerciseBlocked),
            ExerciseVideoRegistrationStatus.UploadInProgress =>
                Result<ExerciseVideoUploadDto>.Failure(ExerciseVideoErrors.UploadInProgress),
            ExerciseVideoRegistrationStatus.QuotaExceeded =>
                Result<ExerciseVideoUploadDto>.Failure(ExerciseVideoErrors.QuotaExceeded),
            _ => throw new ArgumentOutOfRangeException(nameof(registration), registration, null)
        };
    }
}
