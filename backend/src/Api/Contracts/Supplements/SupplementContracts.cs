using Application.Features.Supplements.Dtos;

namespace Api.Contracts.Supplements;

/// <summary>Criação de um suplemento privado.</summary>
public sealed record CreateSupplementRequest(
    string Name,
    string? Description,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes);

/// <summary>Substitui os campos editáveis de um suplemento privado.</summary>
public sealed record UpdateSupplementRequest(
    string Name,
    string? Description,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes);

/// <summary>Suplemento visível ao personal trainer, global ou privado.</summary>
public sealed record SupplementResponse(
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
    DateTime UpdatedAt)
{
    /// <summary>Projeta o DTO da Application no contrato da Api.</summary>
    public static SupplementResponse From(SupplementDto supplement)
    {
        ArgumentNullException.ThrowIfNull(supplement);

        return new(
            supplement.Id,
            supplement.Scope,
            supplement.Name,
            supplement.Description,
            supplement.UnitOfMeasure,
            supplement.ServingSize,
            supplement.Timing,
            supplement.TrainerNotes,
            supplement.IsActive,
            supplement.CreatedAt,
            supplement.UpdatedAt);
    }
}

/// <summary>Dados editáveis de um suplemento global novo.</summary>
public sealed record CreateGlobalSupplementRequest(
    string Name,
    string? Description,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes);

/// <summary>Substitui os campos editáveis de um suplemento global.</summary>
public sealed record UpdateGlobalSupplementRequest(
    string Name,
    string? Description,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes);

/// <summary>Suplemento global apresentado ao superuser.</summary>
public sealed record GlobalSupplementResponse(
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
    DateTime UpdatedAt)
{
    /// <summary>Projeta o DTO da Application no contrato da Api.</summary>
    public static GlobalSupplementResponse From(GlobalSupplementDto supplement)
    {
        ArgumentNullException.ThrowIfNull(supplement);

        return new(
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
            supplement.UpdatedAt);
    }
}

/// <summary>Atribui um suplemento a um cliente do tenant.</summary>
public sealed record AssignSupplementRequest(
    Guid ClientId,
    Guid SupplementId,
    string? ServingSize,
    string? Timing,
    string? TrainerNotes);

/// <summary>Ajusta a prescrição de uma atribuição existente.</summary>
public sealed record UpdateSupplementAssignmentRequest(
    string ServingSize,
    string Timing,
    string? TrainerNotes);

/// <summary>Atribuição de suplemento, na perspetiva do personal trainer.</summary>
public sealed record ClientSupplementAssignmentResponse(
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
    DateTime UpdatedAt)
{
    /// <summary>Projeta o DTO da Application no contrato da Api.</summary>
    public static ClientSupplementAssignmentResponse From(
        ClientSupplementAssignmentDto assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return new(
            assignment.Id,
            assignment.ClientId,
            assignment.SupplementId,
            assignment.SupplementName,
            assignment.SupplementDescription,
            assignment.UnitOfMeasure,
            assignment.ServingSize,
            assignment.Timing,
            assignment.TrainerNotes,
            assignment.IsActive,
            assignment.IsSupplementArchived,
            assignment.CreatedAt,
            assignment.UpdatedAt);
    }
}
