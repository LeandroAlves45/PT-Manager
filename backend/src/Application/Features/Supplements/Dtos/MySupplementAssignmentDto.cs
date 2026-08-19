namespace Application.Features.Supplements.Dtos;

/// <summary>Prescrição ativa apresentada ao cliente autenticado.</summary>
public sealed record MySupplementAssignmentDto(
    Guid Id,
    Guid SupplementId,
    string SupplementName,
    string? SupplementDescription,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes,
    bool IsSupplementArchived,
    DateTime UpdatedAt
);
