using Application.Features.Training.Exercises.Dtos;

namespace Api.Contracts.Training;

/// <summary>Dados editáveis de um exercício privado.</summary>
public sealed record CreateExerciseRequest(
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl);

/// <summary>Substitui os campos editáveis de um exercício privado.</summary>
public sealed record UpdateExerciseRequest(
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl);

/// <summary>Exercício visível ao personal trainer, global ou privado.</summary>
public sealed record ExerciseResponse(
    Guid Id,
    string Scope,
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl,
    bool IsActive,
    string PlatformEnforcementStatus,
    string? PlatformEnforcementReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    /// <summary>Projeta o DTO da Application no contrato da API.</summary>
    public static ExerciseResponse From(ExerciseDto exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return new(
            exercise.Id,
            exercise.Scope,
            exercise.Name,
            exercise.Description,
            exercise.MuscleGroups,
            exercise.Equipment,
            exercise.DifficultyLevel,
            exercise.VideoUrl,
            exercise.IsActive,
            exercise.PlatformEnforcementStatus,
            exercise.PlatformEnforcementReason,
            exercise.CreatedAt,
            exercise.UpdatedAt
        );
    }
}

/// <summary>Dados editáveis de um exercício global novo.</summary>
public sealed record CreateGlobalExerciseRequest(
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl);

/// <summary>Substitui os campos editáveis de um exercício global.</summary>
public sealed record UpdateGlobalExerciseRequest(
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl);

/// <summary>Exercício global apresentado ao superuser.</summary>
public sealed record GlobalExerciseResponse(
    Guid Id,
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o DTO da Application no contrato da API.</summary>
    public static GlobalExerciseResponse From(GlobalExerciseDto exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return new(
            exercise.Id,
            exercise.Name,
            exercise.Description,
            exercise.MuscleGroups,
            exercise.Equipment,
            exercise.DifficultyLevel,
            exercise.VideoUrl,
            exercise.IsActive,
            exercise.CreatedAt,
            exercise.UpdatedAt
        );
    }
}
