namespace Application.Features.Supplements.Dtos;

/// <summary>Toma registada hoje pelo cliente para uma atribuição.</summary>
public sealed record MySupplementIntakeDto(
    Guid AssignmentId,
    DateOnly LocalDate,
    DateTime TakenAt
);
