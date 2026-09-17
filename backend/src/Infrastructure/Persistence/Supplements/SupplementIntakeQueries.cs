using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Infrastructure.Data;
using Infrastructure.Persistence.ClientPortal;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Supplements;

/// <summary>Lista as tomas de um dia do cliente autenticado em duas leituras.</summary>
internal sealed class SupplementIntakeQueries : ISupplementIntakeQueries
{
    private readonly PtManagerDbContext _dbContext;

    public SupplementIntakeQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<MyTodaySupplementIntakesDto?> ListMyForDateAsync(
        Guid trainerId,
        Guid clientUserId,
        DateOnly localDate,
        CancellationToken cancellationToken)
    {
        var clientId = await PortalClients.FindActiveIdAsync(
            _dbContext, trainerId, clientUserId, cancellationToken);
        if (!clientId.HasValue)
            return null;

        // Sem paginação: as atribuições ativas estão limitadas a uma por suplemento
        // (uq_client_supplement_active) e o ecrã mostra sempre o dia inteiro.
        var items = await (
            from assignment in _dbContext.ClientSupplementAssignments.AsNoTracking()
            join supplement in _dbContext.Supplements.AsNoTracking()
                on assignment.SupplementId equals supplement.Id
            where assignment.OwnerTrainerId == trainerId &&
                assignment.ClientId == clientId.Value &&
                assignment.IsActive
            orderby supplement.Name, assignment.Id
            select new
            {
                assignment.Id,
                supplement.Name,
                assignment.ServingSize,
                assignment.Timing,
                TakenAt = _dbContext.ClientSupplementIntakes
                    .Where(intake =>
                        intake.ClientSupplementAssignmentId == assignment.Id &&
                        intake.LocalDate == localDate)
                    .Select(intake => (DateTime?)intake.TakenAt)
                    .FirstOrDefault()
            }
        ).ToListAsync(cancellationToken);

        var dtos = items
            .Select(item => new MyTodaySupplementIntakesDto.ItemDto(
                item.Id,
                item.Name,
                item.ServingSize,
                item.Timing,
                item.TakenAt.HasValue,
                item.TakenAt))
            .ToArray();

        return new MyTodaySupplementIntakesDto(
            localDate,
            dtos.Count(item => item.IsTaken),
            dtos.Length,
            dtos);
    }
}
