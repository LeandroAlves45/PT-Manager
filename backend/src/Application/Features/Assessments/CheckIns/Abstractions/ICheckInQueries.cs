using Application.Features.Assessments.CheckIns.Dtos;
using Application.Features.Assessments.CheckIns.ListCheckIns;
using Application.Pagination;

namespace Application.Features.Assessments.CheckIns.Abstractions;

/// <summary>Consulta check-ins sem tracking e com identidade explícita.</summary>
public interface ICheckInQueries
{
    Task<CheckInDto?> GetAsync(
        Guid trainerId,
        Guid checkInId,
        DateOnly localToday,
        CancellationToken cancellationToken
    );

    Task<PageResult<CheckInDto>> ListAsync(
        Guid trainerId,
        Guid? clientId,
        CheckInStatusFilter? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        DateOnly localToday,
        PageRequest page,
        CancellationToken cancellationToken
    );

    Task<CheckInDto?> GetMyDueAsync(
        Guid trainerId,
        Guid userId,
        DateOnly localToday,
        CancellationToken cancellationToken
    );
}
