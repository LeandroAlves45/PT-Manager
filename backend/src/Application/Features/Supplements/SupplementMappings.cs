using Application.Features.Supplements.Dtos;
using Domain.Entities.Supplements;

namespace Application.Features.Supplements;

/// <summary>Converte entidades de suplementos em DTOs da Application.</summary>
public static class SupplementMappings
{
    public static SupplementDto ToDto(this Supplement supplement)
    {
        ArgumentNullException.ThrowIfNull(supplement);

        return new SupplementDto(
            supplement.Id,
            supplement.OwnerTrainerId.HasValue ? "private" : "global",
            supplement.Name,
            supplement.Description,
            supplement.UnitOfMeasure,
            supplement.ServingSize,
            supplement.Timing,
            supplement.TrainerNotes,
            supplement.IsActive,
            supplement.CreatedAt,
            supplement.UpdatedAt
        );
    }

    public static GlobalSupplementDto ToGlobalDto(this Supplement supplement)
    {
        ArgumentNullException.ThrowIfNull(supplement);
        if (supplement.OwnerTrainerId.HasValue)
            throw new ArgumentException(
                "A private supplement cannot be mapped as global.", nameof(supplement));

        return new GlobalSupplementDto(
            supplement.Id,
            supplement.CreatedByUserId,
            supplement.Name,
            supplement.Description,
            supplement.UnitOfMeasure,
            supplement.ServingSize,
            supplement.Timing,
            supplement.TrainerNotes,
            supplement.IsActive,
            supplement.CreatedAt,
            supplement.UpdatedAt
        );
    }

    public static ClientSupplementAssignmentDto ToDto(
        this ClientSupplementAssignment assignment,
        Supplement supplement
    )
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(supplement);

        return new ClientSupplementAssignmentDto(
            assignment.Id,
            assignment.ClientId,
            supplement.Id,
            supplement.Name,
            supplement.Description,
            supplement.UnitOfMeasure,
            assignment.ServingSize,
            assignment.Timing,
            assignment.TrainerNotes,
            assignment.IsActive,
            !supplement.IsActive,
            assignment.CreatedAt,
            assignment.UpdatedAt
        );
    }
}
