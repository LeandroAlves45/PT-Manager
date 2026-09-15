namespace Application.Features.Training.ExerciseVideos.Abstractions;

/// <summary>
/// Projeção mínima necessária para emitir a URL assinada.
/// Usado para obter um vídeo Ready do exercício quando a audiência o pode ver.
/// </summary>
public sealed record ExerciseVideoPlaybackCandidate(
    Guid VideoId,
    Guid ExerciseId,
    Guid? OwnerTrainerId,
    string ObjectKey,
    string ContentType,
    long DurationMilliseconds,
    int Width,
    int Height,
    bool ExerciseBlocked);
