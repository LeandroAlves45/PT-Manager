namespace Application.Features.Training.Exercises.ReactivateExercise;

/// <summary>Solicita a reativação idempotente de um exercício privado.</summary>
public sealed record ReactivateExerciseCommand(Guid ExerciseId);
