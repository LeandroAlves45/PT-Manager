using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Time;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Features.ClientPortal.GetMyWorkoutToday;
using Application.Features.Supplements.Abstractions;
using Application.Results;

namespace Application.Features.ClientPortal.GetMyPortalHome;

/// <summary>Pede o resumo dos cartões da home do portal.</summary>
public sealed record GetMyPortalHomeQuery;

/// <summary>
/// Agrega os quatro cartões da home reutilizando as leituras já existentes (treino de hoje,
/// plano alimentar ativo, tomas do dia e próximo check-in), com um único cálculo de fuso.
/// </summary>
public sealed class GetMyPortalHomeHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly MyWorkoutTodayReader _workoutReader;
    private readonly IMyNutritionPlanQueries _nutritionQueries;
    private readonly ISupplementIntakeQueries _intakeQueries;
    private readonly ICheckInQueries _checkInQueries;

    public GetMyPortalHomeHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        MyWorkoutTodayReader workoutReader,
        IMyNutritionPlanQueries nutritionQueries,
        ISupplementIntakeQueries intakeQueries,
        ICheckInQueries checkInQueries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _workoutReader = workoutReader ?? throw new ArgumentNullException(nameof(workoutReader));
        _nutritionQueries = nutritionQueries ?? throw new ArgumentNullException(nameof(nutritionQueries));
        _intakeQueries = intakeQueries ?? throw new ArgumentNullException(nameof(intakeQueries));
        _checkInQueries = checkInQueries ?? throw new ArgumentNullException(nameof(checkInQueries));
    }

    public async Task<Result<MyPortalHomeDto>> HandleAsync(
        GetMyPortalHomeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyPortalHomeDto>.Failure(actor.Error!);

        var trainerId = actor.Value.TrainerId;
        var userId = actor.Value.UserId;

        var timeZone = await _timeZoneProvider.GetRequiredAsync(trainerId, cancellationToken);
        var localDate = LocalDates.Today(_clock.UtcNow, timeZone);
        var range = LocalDates.ToUtcRange(localDate, localDate.AddDays(1), timeZone);

        // As tomas são a única leitura que distingue "cliente inexistente ou arquivado" de
        // "sem dados", por isso servem de porteiro da home.
        var intakes = await _intakeQueries.ListMyForDateAsync(
            trainerId,
            userId,
            localDate,
            cancellationToken);
        if (intakes is null)
            return Result<MyPortalHomeDto>.Failure(ClientPortalErrors.ProfileNotAvailable);

        var workout = await _workoutReader.ReadAsync(
            trainerId,
            userId,
            localDate,
            range.StartUtc,
            range.EndUtc,
            cancellationToken);
        var nutrition = await _nutritionQueries.GetActiveAsync(trainerId, userId, cancellationToken);
        var nextCheckIn = await _checkInQueries.GetMyNextAsync(
            trainerId,
            userId,
            localDate,
            cancellationToken);

        return Result<MyPortalHomeDto>.Success(new MyPortalHomeDto(
            localDate,
            workout is null
                ? null
                : new MyPortalHomeDto.WorkoutCardDto(
                    workout.Status,
                    workout.WeekNumber,
                    workout.DayOfWeek,
                    workout.Day?.Notes,
                    workout.Progress.PlannedExercises,
                    workout.Progress.PlannedSets,
                    workout.Progress.LoggedSets,
                    workout.CompletedAt.HasValue,
                    workout.NextWorkout),
            nutrition is null
                ? null
                : new MyPortalHomeDto.NutritionCardDto(
                    nutrition.Id,
                    nutrition.Name,
                    nutrition.TargetKcal,
                    nutrition.Meals.Count),
            new MyPortalHomeDto.SupplementsCardDto(intakes.TakenCount, intakes.TotalCount),
            nextCheckIn is null
                ? null
                : new MyPortalHomeDto.NextCheckInCardDto(
                    nextCheckIn.Id,
                    nextCheckIn.CheckInDate,
                    nextCheckIn.IsToday)));
    }
}
