namespace Application.Features.Training.Exercises.ReactivateGlobalExercise;

/// <summary>Identifica o exercício global a ser reativado.</summary>
public sealed record ReactivateGlobalExerciseCommand(Guid ExerciseId);
