namespace Application.Features.Training.Exercises.Abstractions;

/// <summary>
/// Linha válida do CSV de exercícios, pronta a gravar. Os grupos musculares já vêm
/// normalizados para os códigos do catálogo.
/// </summary>
public sealed record GlobalExerciseImportItem(
    int Line,
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel);
