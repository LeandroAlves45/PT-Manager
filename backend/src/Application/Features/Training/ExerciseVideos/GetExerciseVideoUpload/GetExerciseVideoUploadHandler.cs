namespace Application.Features.Training.ExerciseVideos.GetExerciseVideoUpload;

/// <summary>Consulta o estado técnico de um upload.</summary>
public sealed record GetExerciseVideoUploadQuery(
    ExerciseVideoCatalog Catalog,
    Guid ExerciseId,
    Guid VideoId);
