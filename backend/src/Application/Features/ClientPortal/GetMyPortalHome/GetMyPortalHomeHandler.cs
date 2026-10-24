using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Time;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Features.ClientPortal.GetMyWorkoutToday;
using Application.Features.ClientPortal.PlanSupplementIntakes;
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
    private readonly IInitialAssessmentQueries _initialAssessmentQueries;
    private readonly IMyPlanSupplementIntakeQueries _planSupplementQueries;
    private readonly IAccountAccessQueries _accessQueries;

    public GetMyPortalHomeHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        MyWorkoutTodayReader workoutReader,
        IMyNutritionPlanQueries nutritionQueries,
        ISupplementIntakeQueries intakeQueries,
        ICheckInQueries checkInQueries,
        IInitialAssessmentQueries initialAssessmentQueries,
        IMyPlanSupplementIntakeQueries planSupplementQueries,
        IAccountAccessQueries accessQueries)
    {
        _accessQueries = accessQueries ?? throw new ArgumentNullException(nameof(accessQueries));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _workoutReader = workoutReader ?? throw new ArgumentNullException(nameof(workoutReader));
        _nutritionQueries = nutritionQueries ?? throw new ArgumentNullException(nameof(nutritionQueries));
        _intakeQueries = intakeQueries ?? throw new ArgumentNullException(nameof(intakeQueries));
        _checkInQueries = checkInQueries ?? throw new ArgumentNullException(nameof(checkInQueries));
        _initialAssessmentQueries = initialAssessmentQueries ??
            throw new ArgumentNullException(nameof(initialAssessmentQueries));
        _planSupplementQueries = planSupplementQueries ??
            throw new ArgumentNullException(nameof(planSupplementQueries));
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
            timeZone,
            cancellationToken);
        // A lista basta ao cartão: o plano escolhido vem marcado, sem carregar as refeições.
        var mealPlans = await _nutritionQueries.ListActiveAsync(trainerId, userId, cancellationToken);
        var nutrition = mealPlans.FirstOrDefault(plan => plan.IsSelected);
        var nextCheckIn = await _checkInQueries.GetMyNextAsync(
            trainerId,
            userId,
            localDate,
            cancellationToken);
        // Os suplementos do plano escolhido contam no cartão com as atribuições diretas; sem
        // escolha não há tomas do plano. Os indisponíveis não se marcam e não contam.
        var takenCount = intakes.TakenCount;
        var totalCount = intakes.TotalCount;
        if (nutrition is not null)
        {
            var plan = await _planSupplementQueries.GetForDateAsync(trainerId, userId, localDate, cancellationToken);
            var available = plan.Today?.Items.Where(item => !item.IsUnavailable).ToArray() ?? [];
            takenCount += available.Count(item => item.TakenAt.HasValue);
            totalCount += available.Length;
        }

        // Depois dos 7 dias o pedido já nem chega aqui (o acesso acabou); antes, o aviso.
        var blockedAt = await _accessQueries.GetTrainerBlockedAtAsync(trainerId, cancellationToken);
        var accessEndsAt = blockedAt?.AddDays(Domain.Entities.Administration.TrainerAccessBlock.ClientGraceDays);
        var accessNotice = accessEndsAt > _clock.UtcNow
            ? new MyPortalHomeDto.AccessNoticeDto(accessEndsAt.Value, timeZone.Id)
            : null;

        var initialAssessmentPending = await _initialAssessmentQueries.IsPendingForClientUserAsync(
            trainerId,
            userId,
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
                    nutrition.MealCount),
            mealPlans.Count,
            new MyPortalHomeDto.SupplementsCardDto(takenCount, totalCount),
            nextCheckIn is null
                ? null
                : new MyPortalHomeDto.NextCheckInCardDto(
                    nextCheckIn.Id,
                    nextCheckIn.CheckInDate,
                    nextCheckIn.IsToday),
            initialAssessmentPending == true,
            accessNotice));
    }
}
