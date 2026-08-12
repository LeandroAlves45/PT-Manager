namespace Application.Features.Training.Exercises.ArchiveExercise;

/// <summary>Solicita o arquivo idempotente de um exercício privado.</summary>
public sealed record ArchiveExerciseCommand(Guid ExerciseId);
