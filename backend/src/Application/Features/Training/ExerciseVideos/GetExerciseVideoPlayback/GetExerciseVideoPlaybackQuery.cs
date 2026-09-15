using Application.Common.Abstractions;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Application.Features.Training.ExerciseVideos.Dtos;
using Application.Results;

namespace Application.Features.Training.ExerciseVideos.GetExerciseVideoPlayback;

/// <summary>
/// Emite uma URL assinada de curta duração para um vídeo Ready visível à
/// audiência.
/// </summary>
/// <remarks>
/// Só existe URL para vídeos Ready: um vídeo pendente, em processamento ou
/// recusado continua privado. Um exercício bloqueado pela plataforma não é
/// reproduzível fora do contexto administrativo, e a resposta é igual à de um
/// vídeo inexistente para não revelar o bloqueio.
/// </remarks>
public sealed class GetExerciseVideoPlaybackHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ExerciseVideoSettings _settings;
    private readonly IExerciseVideoQueries _queries;
    private readonly IVideoObjectStorage _storage;

    public GetExerciseVideoPlaybackHandler(
        ITenantContext tenantContext,
        IClock clock,
        ExerciseVideoSettings settings,
        IExerciseVideoQueries queries,
        IVideoObjectStorage storage)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public async Task<Result<ExerciseVideoPlaybackDto>> HandleAsync(
        GetExerciseVideoPlaybackQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ExerciseId == Guid.Empty)
            return Result<ExerciseVideoPlaybackDto>.Failure(TrainingErrors.ExerciseIdRequired());

        var actor = ExerciseVideoActors.ResolveReader(_tenantContext, query.Audience);
        if (!actor.IsSuccess)
            return Result<ExerciseVideoPlaybackDto>.Failure(actor.Error!);

        var candidate = await _queries.FindPlaybackCandidateAsync(
            query.Audience,
            query.ExerciseId,
            actor.Value.TrainerId,
            actor.Value.ClientUserId,
            cancellationToken);

        if (candidate is null ||
            (candidate.ExerciseBlocked &&
                query.Audience != ExerciseVideoPlaybackAudience.Administrative))
            return Result<ExerciseVideoPlaybackDto>.Failure(ExerciseVideoErrors.VideoNotFound);

        var playback = await _storage.CreatePlaybackUrlAsync(
            candidate.ObjectKey,
            candidate.OwnerTrainerId,
            _clock.UtcNow.Add(_settings.PlaybackUrlLifetime),
            cancellationToken);

        if (playback.Status != VideoStorageStatus.Success || playback.Playback is null)
            return Result<ExerciseVideoPlaybackDto>.Failure(ExerciseVideoErrors.StorageUnavailable);

        return Result<ExerciseVideoPlaybackDto>.Success(new ExerciseVideoPlaybackDto(
            candidate.VideoId,
            candidate.ExerciseId,
            candidate.ContentType,
            candidate.DurationMilliseconds,
            candidate.Width,
            candidate.Height,
            playback.Playback.Url,
            playback.Playback.ExpiresAt));
    }
}
