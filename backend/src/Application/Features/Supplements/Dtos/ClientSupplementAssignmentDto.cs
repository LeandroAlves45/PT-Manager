namespace Application.Features.Supplements.Dtos;

/// <summary>Atribuição com dados públicos do suplemento e instruções personalizadas.</summary>
public sealed record ClientSupplementAssignmentDto(
    Guid Id,
    Guid ClientId,
    Guid SupplementId,
    string SupplementName,
    string? SupplementDescription,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes,
    bool IsActive,
    bool IsSupplementArchived,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
