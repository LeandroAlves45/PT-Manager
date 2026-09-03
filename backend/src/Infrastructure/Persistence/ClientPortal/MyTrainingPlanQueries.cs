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
                            .OrderBy(row => row.item.OrderNumber)
                            .ThenBy(row => row.item.GroupPosition)
                            .ThenBy(row => row.item.Id)
                            .Select(row => new MyTrainingPlanDto.ExerciseDto(
                                row.item.OrderNumber,
                                row.catalog.PlatformEnforcementStatus ==
                                    PlatformEnforcementStatus.Blocked
                                    ? PortalContentMasking.UnavailableExerciseName
                                    : row.catalog.Name,
                                row.catalog.PlatformEnforcementStatus ==
                                    PlatformEnforcementStatus.Blocked,
                                row.item.ExerciseGroupId,
                                row.item.GroupPosition,
                                row.item.Notes,
                                _dbContext.ExerciseSets
                                    .Where(set =>
                                        set.TrainingPlanDayExerciseId == row.item.Id)
                                    .OrderBy(set => set.SetNumber)
                                    .ThenBy(set => set.Id)
                                    .Select(set => new MyTrainingPlanDto.SetDto(
                                        set.SetNumber,
                                        set.PlannedReps,
                                        set.PlannedWeightKg,
                                        set.RestSecondsMin,
                                        set.RestSecondsMax))
                                    .ToList()))
                            .ToList()))
                    .ToList(),
                plan.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
}
