namespace Application.Features.Supplements.ListSupplementAssignments;

/// <summary>Lista atribuições do tenant, opcionalmente por cliente.</summary>
public sealed record ListSupplementAssignmentsQuery(
    Guid? ClientId,
    SupplementAssignmentActivityFilter Activity = SupplementAssignmentActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50
);
