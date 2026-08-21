namespace Application.Features.Training.Exercises.Dtos;

/// <summary>Exercício global apresentado a um superuser autorizado.</summary>
public sealed record GlobalExerciseDto(
    Guid Id,
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
