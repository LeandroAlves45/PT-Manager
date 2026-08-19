namespace Application.Features.Supplements.Dtos;

/// <summary>Suplemento global apresentado a um superuser autorizado.</summary>
public sealed record GlobalSupplementDto(
    Guid Id,
    Guid CreatedByUserId,
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
