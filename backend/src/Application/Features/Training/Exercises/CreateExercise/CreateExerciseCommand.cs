namespace Application.Features.Training.Exercises.CreateExercise;

/// <summary>Solicita a criação de um exercício privado.</summary>
public sealed record CreateExerciseCommand(
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl
);
