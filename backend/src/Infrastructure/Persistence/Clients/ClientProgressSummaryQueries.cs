using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Clients;

/// <summary>
/// Lê o retrato do cliente em oito comandos fixos: existência, dois pesos de check-in,
/// avaliação inicial, plano de treino ativo com estrutura, séries da janela, plano alimentar
/// ativo e saldo de packs. Tudo sob os Global Query Filters do tenant.
/// </summary>
internal sealed class ClientProgressSummaryQueries : IClientProgressSummaryQueries
{
    private readonly PtManagerDbContext _dbContext;

    public ClientProgressSummaryQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<ClientProgressSnapshot?> GetAsync(
        Guid clientId,
        DateOnly weightWindowStart,
        DateTimeOffset logsFromUtc,
        DateTimeOffset logsToUtc,
        CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Clients
            .AsNoTracking()
            .AnyAsync(client => client.Id == clientId, cancellationToken);

        if (!exists)
            return null;

        var answeredWeights = _dbContext.CheckIns
            .AsNoTracking()
            .Where(checkIn => checkIn.ClientId == clientId)
            .Where(checkIn =>
                checkIn.RespondedAt.HasValue &&
                !checkIn.CancelledAt.HasValue &&
                checkIn.WeightKg.HasValue);

        var latestWeight = await answeredWeights
            .OrderByDescending(checkIn => checkIn.CheckInDate)
            .ThenByDescending(checkIn => checkIn.Id)
            .Select(checkIn => new CheckInWeightRow(checkIn.CheckInDate, checkIn.WeightKg!.Value))
            .FirstOrDefaultAsync(cancellationToken);

        var earliestWeight = await answeredWeights
            .Where(checkIn => checkIn.CheckInDate >= weightWindowStart)
            .OrderBy(checkIn => checkIn.CheckInDate)
            .ThenBy(checkIn => checkIn.Id)
            .Select(checkIn => new CheckInWeightRow(checkIn.CheckInDate, checkIn.WeightKg!.Value))
            .FirstOrDefaultAsync(cancellationToken);

        var initialAssessment = await _dbContext.InitialAssessments
            .AsNoTracking()
            .Where(assessment => assessment.ClientId == clientId)
            .Select(assessment => new InitialAssessmentRow(
                assessment.WeightKg,
                assessment.HeightCm,
                assessment.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        var plan = await _dbContext.TrainingPlans
            .AsNoTracking()
            .Where(item => item.ClientId == clientId && item.IsActive && !item.IsArchived)
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(item => new ActiveTrainingPlanStructure(
                item.Id,
                item.Name,
                item.StartDate,
                item.EndDate,
                _dbContext.TrainingPlanDays
                    .Where(day => day.TrainingPlanId == item.Id)
                    .OrderBy(day => day.WeekNumber)
                    .ThenBy(day => day.DayOfWeek)
                    .Select(day => new PlannedDayRow(
                        day.WeekNumber,
                        day.DayOfWeek,
                        _dbContext.ExerciseSets
                            .Where(set => _dbContext.TrainingPlanDayExercises.Any(prescription =>
                                prescription.Id == set.TrainingPlanDayExerciseId &&
                                prescription.TrainingPlanDayId == day.Id))
                            .Select(set => new PlannedSetRow(
                                set.TrainingPlanDayExerciseId,
                                set.SetNumber))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        var logs = plan is null
            ? new List<PerformedSetRow>()
            : await _dbContext.ClientExerciseSetLogs
                .AsNoTracking()
                .Where(log => log.ClientId == clientId)
                .Where(log => log.PerformedAt >= logsFromUtc && log.PerformedAt < logsToUtc)
                .Where(log => _dbContext.TrainingPlanDayExercises.Any(prescription =>
                    prescription.Id == log.TrainingPlanDayExerciseId &&
                    _dbContext.TrainingPlanDays.Any(day =>
                        day.Id == prescription.TrainingPlanDayId &&
                        day.TrainingPlanId == plan.Id)))
                .Select(log => new PerformedSetRow(
                    log.TrainingPlanDayExerciseId,
                    log.SetNumber,
                    log.PerformedAt))
                .ToListAsync(cancellationToken);

        var mealPlan = await _dbContext.MealPlans
            .AsNoTracking()
            .Where(item => item.ClientId == clientId && item.IsActive && !item.IsArchived)
            .OrderByDescending(item => item.StartsDate)
            .ThenByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(item => new ClientProgressSummaryDto.NutritionDto(
                item.Id,
                item.Name,
                item.KcalTarget,
                item.Targets.ProteinGrams,
                item.Targets.CarbsGrams,
                item.Targets.FatsGrams))
            .FirstOrDefaultAsync(cancellationToken);

        var packs = await _dbContext.ClientSessionPacks
            .AsNoTracking()
            .Where(pack => pack.ClientId == clientId && pack.SessionsRemaining > 0)
            .GroupBy(_ => 1)
            .Select(group => new ClientProgressSummaryDto.PacksDto(
                group.Count(),
                group.Sum(pack => pack.SessionsRemaining),
                group.Min(pack => pack.ExpectedEndDate)))
            .FirstOrDefaultAsync(cancellationToken);

        return new ClientProgressSnapshot(
            clientId,
            latestWeight,
            earliestWeight,
            initialAssessment,
            plan,
            logs,
            mealPlan,
            packs ?? new ClientProgressSummaryDto.PacksDto(0, 0, null));
    }
}
