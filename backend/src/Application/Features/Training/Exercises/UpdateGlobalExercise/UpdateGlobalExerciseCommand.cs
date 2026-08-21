namespace Application.Features.Training.Exercises.UpdateGlobalExercise;

/// <summary>Dados editáveis de um exercício global existente.</summary>
public sealed record UpdateGlobalExerciseCommand(
    Guid ExerciseId,
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl
);
