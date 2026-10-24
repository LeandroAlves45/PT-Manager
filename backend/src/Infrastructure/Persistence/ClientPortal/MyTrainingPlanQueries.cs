using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>
/// Projeta o plano de treino ativo do cliente autenticado.
/// </summary>
internal sealed class MyTrainingPlanQueries : IMyTrainingPlanQueries
{
    private readonly PtManagerDbContext _dbContext;

    public MyTrainingPlanQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<MyTrainingPlanDto?> GetActiveAsync(
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken) =>
        _dbContext.TrainingPlans
            .AsNoTracking()
            .AsSingleQuery()
            .Where(plan =>
                plan.OwnerTrainerId == trainerId &&
                plan.IsActive &&
                !plan.IsArchived &&
                _dbContext.Clients.Any(client =>
                    client.Id == plan.ClientId &&
                    client.OwnerTrainerId == trainerId &&
                    client.UserId == clientUserId &&
                    client.IsActive))
            .OrderByDescending(plan => plan.StartDate)
            .ThenByDescending(plan => plan.CreatedAt)
            .ThenBy(plan => plan.Id)
            .Select(plan => new MyTrainingPlanDto(
                plan.Id,
                plan.Name,
                plan.Description,
                plan.TrainingModality,
                plan.Notes,
                plan.StartDate,
                plan.EndDate,
                _dbContext.TrainingPlanDays
                    .Where(day => day.TrainingPlanId == plan.Id)
                    .OrderBy(day => day.WeekNumber)
                    .ThenBy(day => day.DayOfWeek)
                    .ThenBy(day => day.Id)
                    .Select(day => new MyTrainingPlanDto.DayDto(
                        day.Id,
                        day.DayOfWeek,
                        day.WeekNumber,
                        day.Notes,
                        _dbContext.TrainingPlanDayExercises
                            .Where(item => item.TrainingPlanDayId == day.Id)
                            .Join(
                                _dbContext.Exercises,
                                item => item.ExerciseId,
                                catalog => catalog.Id,
                                (item, catalog) => new { item, catalog })
                            // O exercício bloqueado e o retirado do plano (com séries já
                            // registadas) saem do plano do cliente.
                            .Where(row =>
                                row.catalog.PlatformEnforcementStatus != PlatformEnforcementStatus.Blocked &&
                                row.item.RetiredAt == null)
                            .OrderBy(row => row.item.OrderNumber)
                            .ThenBy(row => row.item.GroupPosition)
                            .ThenBy(row => row.item.Id)
                            .Select(row => new MyTrainingPlanDto.ExerciseDto(
                                row.item.Id,
                                row.catalog.Id,
                                row.item.OrderNumber,
                                row.catalog.Name,
                                // Mesma regra do pedido de vídeo do cliente
                                // (ExerciseVideoQueries.ForClient e o handler de playback):
                                // vídeo Ready global ou do próprio trainer; o bloqueado já saiu
                                // acima. Assim has_ready_video nunca promete um 404.
                                _dbContext.ExerciseVideos.Any(video =>
                                    video.ExerciseId == row.catalog.Id &&
                                    video.Status == ExerciseVideoStatus.Ready &&
                                    (video.OwnerTrainerId == null ||
                                        video.OwnerTrainerId == trainerId)),
                                row.item.ExerciseGroupId,
                                row.item.GroupPosition,
                                row.item.Notes,
                                _dbContext.ExerciseSets
                                    .Where(set =>
                                        set.TrainingPlanDayExerciseId == row.item.Id)
                                    .OrderBy(set => set.SetNumber)
                                    .ThenBy(set => set.Id)
                                    .Select(set => new MyTrainingPlanDto.SetDto(
                                        set.Id,
                                        set.SetNumber,
                                        set.PlannedReps,
                                        set.PlannedRepsMax,
                                        set.PlannedWeightKg,
                                        set.RestSecondsMin,
                                        set.RestSecondsMax,
                                        set.PlannedRpe))
                                    .ToList()))
                            .ToList()))
                    .ToList(),
                plan.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
}
