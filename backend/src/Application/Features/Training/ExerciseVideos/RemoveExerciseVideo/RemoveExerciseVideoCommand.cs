namespace Application.Features.Training.ExerciseVideos.RemoveExerciseVideo;

/// <summary>Remove o vídeo Ready de um exercício.</summary>
public sealed record RemoveExerciseVideoCommand(
    ExerciseVideoCatalog Catalog,
    Guid ExerciseId);
