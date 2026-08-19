namespace Application.Features.Supplements.Dtos;

/// <summary>Suplemento global ou privado visível ao personal trainer.</summary>
public sealed record SupplementDto(
    Guid Id,
    string Scope,
    string Name,
    string? Description,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
