namespace Application.Features.Training.ExerciseVideos.RequestExerciseVideoUpload;

/// <summary>Pede autorização para enviar um vídeo diretamente para o storage privado.</summary>
public sealed record RequestExerciseVideoUploadCommand(
    ExerciseVideoCatalog Catalog,
    Guid ExerciseId,
    string ContentType,
    long SizeBytes);
