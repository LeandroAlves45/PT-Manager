namespace Application.Features.Assessments.CheckIns.ListCheckIns;

/// <summary>Lista check-ins do tenant.</summary>
public sealed record ListCheckInsQuery(
    Guid? ClientId,
    CheckInStatusFilter? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int PageNumber = 1,
    int PageSize = 50
);
