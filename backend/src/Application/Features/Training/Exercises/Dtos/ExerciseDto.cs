using Domain.ValueObjects;

namespace Application.Features.Training.Exercises.Dtos;

/// <summary>Representa um exercício global ou privado visível ao personal trainer.</summary>
/// <remarks>
/// <see cref="ManagedVideoStatus"/> é o estado do vídeo gerido mais recente (pode ser uma
/// substituição em curso) e <see cref="HasReadyVideo"/> indica se existe algum vídeo pronto a
/// reproduzir. Só as queries os calculam; a criação devolve sempre <c>null</c>/<c>false</c>.
/// </remarks>
public sealed record ExerciseDto(
    Guid Id,
    string Scope,
    string Name,
    string? Description,
    string? MuscleGroups,
    string? Equipment,
    string? DifficultyLevel,
    string? VideoUrl,
    ExerciseVideoStatus? ManagedVideoStatus,
    bool HasReadyVideo,
    bool IsActive,
    string PlatformEnforcementStatus,
    string? PlatformEnforcementReason,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
