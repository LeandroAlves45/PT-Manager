namespace Application.Features.Supplements.Dtos;

/// <summary>
/// Suplemento global ou privado visível ao personal trainer. O privado bloqueado pela moderação
/// traz o estado, o motivo e a nota.
/// </summary>
public sealed record SupplementDto(
    Guid Id,
    string Scope,
    string Name,
    string? Description,
    string UnitOfMeasure,
    string ServingSize,
    string? Timing,
    string? TrainerNotes,
    bool IsActive,
    string PlatformEnforcementStatus,
    string? PlatformEnforcementReason,
    string? PlatformEnforcementNote,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
