namespace Application.Features.Training.ExerciseVideos.GetExerciseVideoPlayback;

/// <summary>Pede uma URL assinada de reprodução para o vídeo Ready do exercício.</summary>
public sealed record GetExerciseVideoPlaybackQuery(
    ExerciseVideoPlaybackAudience Audience,
    Guid ExerciseId);
