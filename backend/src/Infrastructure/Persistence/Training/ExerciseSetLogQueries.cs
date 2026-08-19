using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Dtos;
using Application.Pagination;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Training;

/// <summary>Consulta execuções de séries visíveis no tenant atual.</summary>
internal sealed class ExerciseSetLogQueries : IExerciseSetLogQueries
{
    private readonly PtManagerDbContext _dbContext;

    public ExerciseSetLogQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ClientExerciseSetLogDto?> GetAsync(
        Guid exerciseSetLogId,
        CancellationToken cancellationToken)
    {
        var row = await BuildQuery()
            .Where(row => row.Id == exerciseSetLogId)
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : Map(row);
    }

    public async Task<PageResult<ClientExerciseSetLogDto>> ListAsync(
        Guid clientId,
        Guid? trainingPlanId,
        DateTimeOffset? performedFrom,
        DateTimeOffset? performedTo,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery()
            .Where(row => row.ClientId == clientId);

        if (trainingPlanId.HasValue)
            query = query.Where(row => row.TrainingPlanId == trainingPlanId.Value);
        if (performedFrom.HasValue)
            query = query.Where(row => row.PerformedAt >= performedFrom.Value);
        if (performedTo.HasValue)
            query = query.Where(row => row.PerformedAt <= performedTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.PerformedAt)
            .ThenBy(row => row.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<ClientExerciseSetLogDto>(
            rows.Select(Map).ToArray(),
            totalCount);
    }

    private IQueryable<LogRow> BuildQuery() =>
        from log in _dbContext.ClientExerciseSetLogs.AsNoTracking()
        join dayExercise in _dbContext.TrainingPlanDayExercises.AsNoTracking()
            on log.TrainingPlanDayExerciseId equals dayExercise.Id
        join day in _dbContext.TrainingPlanDays.AsNoTracking()
            on dayExercise.TrainingPlanDayId equals day.Id
        join plan in _dbContext.TrainingPlans.AsNoTracking()
            on day.TrainingPlanId equals plan.Id
        join exercise in _dbContext.Exercises.AsNoTracking()
            on dayExercise.ExerciseId equals exercise.Id
        select new LogRow
        {
            Id = log.Id,
            ClientId = log.ClientId,
            TrainingPlanId = plan.Id,
            TrainingPlanDayId = day.Id,
            TrainingPlanDayExerciseId = dayExercise.Id,
            ExerciseId = exercise.Id,
            ExerciseName = exercise.Name,
            SetNumber = log.SetNumber,
            WeightKg = log.WeightKg,
            RepsDone = log.RepsDone,
            Notes = log.Notes,
            PerformedAt = log.PerformedAt,
            CreatedAt = log.CreatedAt,
            UpdatedAt = log.UpdatedAt
        };

    private static ClientExerciseSetLogDto Map(LogRow row) => new(
        row.Id,
        row.ClientId,
        row.TrainingPlanId,
        row.TrainingPlanDayId,
        row.TrainingPlanDayExerciseId,
        row.ExerciseId,
        row.ExerciseName,
        row.SetNumber,
        row.WeightKg,
        row.RepsDone,
        row.Notes,
        row.PerformedAt,
        row.CreatedAt,
        row.UpdatedAt);

    private sealed class LogRow
    {
        public required Guid Id { get; init; }
        public required Guid ClientId { get; init; }
        public required Guid TrainingPlanId { get; init; }
        public required Guid TrainingPlanDayId { get; init; }
        public required Guid TrainingPlanDayExerciseId { get; init; }
        public required Guid ExerciseId { get; init; }
        public required string ExerciseName { get; init; }
        public required int SetNumber { get; init; }
        public required decimal WeightKg { get; init; }
        public required int RepsDone { get; init; }
        public string? Notes { get; init; }
        public required DateTimeOffset PerformedAt { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }
}
