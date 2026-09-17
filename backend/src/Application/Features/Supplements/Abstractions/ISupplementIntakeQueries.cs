using Application.Features.Supplements.Dtos;

namespace Application.Features.Supplements.Abstractions;

/// <summary>Consulta as tomas do dia do cliente autenticado.</summary>
public interface ISupplementIntakeQueries
{
    Task<MyTodaySupplementIntakesDto?> ListMyForDateAsync(
        Guid trainerId,
        Guid clientUserId,
        DateOnly localDate,
        CancellationToken cancellationToken);
}
