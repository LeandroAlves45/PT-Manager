using Application.Features.ClientPortal.Abstractions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>
/// Leituras do treino de hoje. O cliente é sempre resolvido pelo par (tenant, utilizador
/// autenticado): nenhum identificador de cliente chega do pedido.
/// </summary>
internal sealed class MyWorkoutTodayQueries : IMyWorkoutTodayQueries
{
    private readonly PtManagerDbContext _dbContext;

    public MyWorkoutTodayQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<IReadOnlyList<MyTodaySetLogRow>> ListMyLogsAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid trainingPlanId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken) =>
        await _dbContext.ClientExerciseSetLogs
            .AsNoTracking()
            .Where(log => _dbContext.Clients.Any(client =>
                client.Id == log.ClientId &&
                client.OwnerTrainerId == trainerId &&
                client.UserId == clientUserId &&
                client.IsActive))
            .Where(log => log.PerformedAt >= fromUtc && log.PerformedAt < toUtc)
            .Where(log => _dbContext.TrainingPlanDayExercises.Any(prescription =>
                prescription.Id == log.TrainingPlanDayExerciseId &&
                _dbContext.TrainingPlanDays.Any(day =>
                    day.Id == prescription.TrainingPlanDayId &&
                    day.TrainingPlanId == trainingPlanId)))
            .OrderBy(log => log.TrainingPlanDayExerciseId)
            .ThenBy(log => log.SetNumber)
            .ThenBy(log => log.PerformedAt)
            .Select(log => new MyTodaySetLogRow(
                log.Id,
                log.TrainingPlanDayExerciseId,
                log.SetNumber,
                log.WeightKg,
                log.RepsDone,
                log.Rpe,
                log.PerformedAt))
            .ToListAsync(cancellationToken);

    public async Task<DateTime?> GetMyCompletionAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid trainingPlanId,
        DateOnly localDate,
        CancellationToken cancellationToken) =>
        await _dbContext.WorkoutCompletions
            .AsNoTracking()
            .Where(completion =>
                completion.OwnerTrainerId == trainerId &&
                completion.TrainingPlanDayId == trainingPlanId &&
                completion.LocalDate == localDate)
            .Where(completion => _dbContext.Clients.Any(client =>
                client.Id == completion.ClientId &&
                client.OwnerTrainerId == trainerId &&
                client.UserId == clientUserId &&
                client.IsActive))
            .Select(completion => (DateTime?)completion.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
