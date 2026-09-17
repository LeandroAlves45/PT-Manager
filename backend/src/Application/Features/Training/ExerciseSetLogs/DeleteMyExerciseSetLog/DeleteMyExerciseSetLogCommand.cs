namespace Application.Features.Training.ExerciseSetLogs.DeleteMyExerciseSetLog;

/// <summary>Remove (desmarca) uma série própria registada hoje.</summary>
public sealed record DeleteMyExerciseSetLogCommand(Guid ExerciseSetLogId);
