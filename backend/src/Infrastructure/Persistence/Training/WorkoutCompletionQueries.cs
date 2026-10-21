using Application.Features.Training.WorkoutCompletions.Abstractions;
using Application.Features.Training.WorkoutCompletions.Dtos;
using Application.Pagination;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Training;

/// <summary>
/// Conclusões de treino visíveis no tenant atual. O filtro global de <c>WorkoutCompletion</c>
/// (OwnerTrainerId) isola os personal trainers; aqui só se filtra o cliente e o plano.
/// </summary>
internal sealed class WorkoutCompletionQueries : IWorkoutCompletionQueries
{
    private readonly PtManagerDbContext _dbContext;

    public WorkoutCompletionQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<PageResult<WorkoutCompletionDto>> ListAsync(
        Guid clientId,
        Guid? trainingPlanId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.WorkoutCompletions
            .AsNoTracking()
            .Where(completion => completion.ClientId == clientId);

        if (trainingPlanId.HasValue)
            query = query.Where(completion => completion.TrainingPlanId == trainingPlanId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(completion => completion.LocalDate)
            .ThenByDescending(completion => completion.CompletedAt)
            .ThenBy(completion => completion.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(completion => new WorkoutCompletionDto(
                completion.Id,
                completion.ClientId,
                completion.TrainingPlanId,
                completion.TrainingPlanDayId,
                completion.LocalDate,
                completion.Notes,
                completion.CompletedAt))
            .ToListAsync(cancellationToken);

        return new PageResult<WorkoutCompletionDto>(items, totalCount);
    }
}
