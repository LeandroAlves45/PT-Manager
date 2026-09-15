namespace Application.Features.Training.ExerciseVideos.CompleteExerciseVideoUpload;

/// <summary>Confirma que o browser terminou o upload direto.</summary>
public sealed record CompleteExerciseVideoUploadCommand(
    ExerciseVideoCatalog Catalog,
    Guid ExerciseId,
    Guid VideoId);


