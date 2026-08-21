namespace Application.Features.Training.Exercises.CreateGlobalExercise;

/// <summary>Dados editáveis de um novo exercício global.</summary>
public sealed record CreateGlobalExerciseCommand(
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl
);
