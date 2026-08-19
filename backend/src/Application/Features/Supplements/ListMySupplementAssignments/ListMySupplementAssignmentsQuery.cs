namespace Application.Features.Supplements.ListMySupplementAssignments;

/// <summary>Lista paginada das prescrições ativas do cliente autenticado.</summary>
public sealed record ListMySupplementAssignmentsQuery(
    int PageNumber = 1,
    int PageSize = 50
);
