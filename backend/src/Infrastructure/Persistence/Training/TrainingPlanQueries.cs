using Application.Features.Training.TrainingPlans.Abstractions;
using Application.Features.Training.TrainingPlans.Dtos;
using Application.Features.Training.TrainingPlans.ListTrainingPlans;
using Application.Pagination;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Training;

/// <summary>Executa projeções read-only de planos de treino.</summary>
internal sealed class TrainingPlanQueries : ITrainingPlanQueries
{
    private readonly PtManagerDbContext _dbContext;

    public TrainingPlanQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<TrainingPlanDetailsDto?> GetDetailsAsync(
        Guid trainingPlanId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.TrainingPlans
            .AsNoTracking()
            .Where(value => value.Id == trainingPlanId)
            .Select(value => new
            {
                value.Id,
                value.ClientId,
                value.Name,
                value.Description,
                value.TrainingModality,
                value.Notes,
                value.StartDate,
                value.EndDate,
                value.IsActive,
                value.IsArchived,
                value.CreatedAt,
                value.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (plan is null)
            return null;

        var days = await _dbContext.TrainingPlanDays
            .AsNoTracking()
            .Where(day => day.TrainingPlanId == trainingPlanId)
            .OrderBy(day => day.WeekNumber)
            .ThenBy(day => day.DayOfWeek)
            .ThenBy(day => day.Id)
            .Select(day => new { day.Id, day.DayOfWeek, day.WeekNumber, day.Notes })
            .ToListAsync(cancellationToken);
        var dayIds = days.Select(day => day.Id).ToArray();

        var exercises = await _dbContext.TrainingPlanDayExercises
            .AsNoTracking()
            .Where(item => dayIds.Contains(item.TrainingPlanDayId))
            .Join(
                _dbContext.Exercises.AsNoTracking(),
                item => item.ExerciseId,
                catalog => catalog.Id,
                (item, catalog) => new { item, catalog })
            .OrderBy(row => row.item.OrderNumber)
            .ThenBy(row => row.item.GroupPosition)
            .ThenBy(row => row.item.Id)
            .Select(row => new
            {
                row.item.Id,
                row.item.TrainingPlanDayId,
                row.item.ExerciseId,
                ExerciseName = row.catalog.Name,
                row.item.OrderNumber,
                row.item.ExerciseGroupId,
                row.item.GroupPosition,
                row.item.Notes
            })
            .ToListAsync(cancellationToken);
        var dayExerciseIds = exercises.Select(item => item.Id).ToArray();

        var sets = await _dbContext.ExerciseSets
            .AsNoTracking()
            .Where(set => dayExerciseIds.Contains(set.TrainingPlanDayExerciseId))
            .OrderBy(set => set.SetNumber)
            .ThenBy(set => set.Id)
            .Select(set => new
            {
                set.Id,
                set.TrainingPlanDayExerciseId,
                set.SetNumber,
                set.PlannedReps,
                set.PlannedWeightKg,
                set.RestSecondsMin,
                set.RestSecondsMax
            })
            .ToListAsync(cancellationToken);

        var daysDtos = days.Select(day => new TrainingPlanDetailsDto.TrainingDayDto(
            day.Id,
            day.DayOfWeek,
            day.WeekNumber,
            day.Notes,
            exercises
                .Where(item => item.TrainingPlanDayId == day.Id)
                .Select(item => new TrainingPlanDetailsDto.DayExerciseDto(
                    item.Id,
                    item.ExerciseId,
                    item.ExerciseName,
                    item.OrderNumber,
                    item.ExerciseGroupId,
                    item.GroupPosition,
                    item.Notes,
                    sets
                        .Where(set => set.TrainingPlanDayExerciseId == item.Id)
                        .Select(set => new TrainingPlanDetailsDto.ExerciseSetDto(
                            set.Id,
                            set.SetNumber,
                            set.PlannedReps,
                            set.PlannedWeightKg,
                            set.RestSecondsMin,
                            set.RestSecondsMax))
                        .ToArray()))
                .ToArray()))
            .ToArray();

        return new TrainingPlanDetailsDto(
            plan.Id,
            plan.ClientId,
            plan.Name,
            plan.Description,
            plan.TrainingModality,
            plan.Notes,
            plan.StartDate,
            plan.EndDate,
            plan.IsActive,
            plan.IsArchived,
            daysDtos,
            plan.CreatedAt,
            plan.UpdatedAt);
    }

    public async Task<PageResult<TrainingPlanSummaryDto>> ListAsync(
        Guid? clientId,
        string? search,
        TrainingPlanActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TrainingPlans.AsNoTracking();
        if (clientId.HasValue)
            query = query.Where(plan => plan.ClientId == clientId.Value);

        query = activity switch
        {
            TrainingPlanActivityFilter.Active => query.Where(plan => plan.IsActive),
            TrainingPlanActivityFilter.Archived => query.Where(plan => plan.IsArchived),
            TrainingPlanActivityFilter.All => query,
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikeSearchPattern.Build(search);
            query = query.Where(plan =>
                EF.Functions.ILike(
                    plan.Name, pattern, LikeSearchPattern.LikeEscapeCharacter) ||
                plan.Description != null &&
                    EF.Functions.ILike(
                        plan.Description, pattern, LikeSearchPattern.LikeEscapeCharacter));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(plan => plan.StartDate)
            .ThenByDescending(plan => plan.CreatedAt)
            .ThenBy(plan => plan.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(plan => new TrainingPlanSummaryDto(
                plan.Id,
                plan.ClientId,
                plan.Name,
                plan.Description,
                plan.TrainingModality,
                plan.StartDate,
                plan.EndDate,
                plan.IsActive,
                plan.IsArchived,
                plan.CreatedAt,
                plan.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PageResult<TrainingPlanSummaryDto>(items, totalCount);
    }
}
