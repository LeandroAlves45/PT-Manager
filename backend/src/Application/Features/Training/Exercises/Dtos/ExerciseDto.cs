namespace Application.Features.Training.Exercises.Dtos;

/// <summary>Representa um exercício global ou privado visível ao personal trainer.</summary>
public sealed record ExerciseDto(
    Guid Id,
    string Scope,
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
