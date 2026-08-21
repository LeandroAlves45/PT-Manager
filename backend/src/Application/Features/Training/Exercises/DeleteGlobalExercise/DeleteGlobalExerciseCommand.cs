namespace Application.Features.Training.Exercises.DeleteGlobalExercise;

/// <summary>Identifica o exercício global a eliminar fisicamente.</summary>
public sealed record DeleteGlobalExerciseCommand(Guid ExerciseId);
