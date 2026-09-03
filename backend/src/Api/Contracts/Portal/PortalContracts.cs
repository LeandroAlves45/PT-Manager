using Api.Contracts.Assessments;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Features.ClientPortal.Dtos;
using Application.Features.Clients.Dtos;
using Application.Features.Supplements.Dtos;

namespace Api.Contracts.Portal;

/// <summary>Identidade visual do personal trainer, apresentada no portal.</summary>
public sealed record PortalBrandingResponse(
    string AppName,
    string? LogoUrl,
    string? PrimaryColor,
    string? BodyColor)
{
    /// <summary>Projeta o branding da Application.</summary>
    public static PortalBrandingResponse From(ClientBrandingDto branding)
    {
        ArgumentNullException.ThrowIfNull(branding);

        return new(
            branding.AppName,
            branding.LogoUrl,
            branding.PrimaryColor,
            branding.BodyColor);
    }
}

/// <summary>Série prescrita, tal como o cliente a vê.</summary>
public sealed record MyExerciseSetResponse(
    int SetNumber,
    int? PlannedReps,
    decimal? PlannedWeightKg,
    int? RestSecondsMin,
    int? RestSecondsMax)
{
    /// <summary>Projeta a série do DTO de portal.</summary>
    public static MyExerciseSetResponse From(MyTrainingPlanDto.SetDto set)
    {
        ArgumentNullException.ThrowIfNull(set);

        return new(
            set.SetNumber,
            set.PlannedReps,
            set.PlannedWeightKg,
            set.RestSecondsMin,
            set.RestSecondsMax);
    }
}

/// <summary>Exercício prescrito, com marcador de indisponibilidade.</summary>
public sealed record MyDayExerciseResponse(
    int OrderNumber,
    string ExerciseName,
    bool IsUnavailable,
    Guid? ExerciseGroupId,
    int? GroupPosition,
    string? Notes,
    IReadOnlyList<MyExerciseSetResponse> Sets)
{
    /// <summary>Projeta o exercício do DTO de portal.</summary>
    public static MyDayExerciseResponse From(MyTrainingPlanDto.ExerciseDto exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return new(
            exercise.OrderNumber,
            exercise.ExerciseName,
            exercise.IsUnavailable,
            exercise.ExerciseGroupId,
            exercise.GroupPosition,
            exercise.Notes,
            exercise.Sets.Select(MyExerciseSetResponse.From).ToArray());
    }
}

/// <summary>Dia de treino visível ao cliente.</summary>
public sealed record MyTrainingDayResponse(
    int DayOfWeek,
    int WeekNumber,
    string? Notes,
    IReadOnlyList<MyDayExerciseResponse> Exercises)
{
    /// <summary>Projeta o dia do DTO de portal.</summary>
    public static MyTrainingDayResponse From(MyTrainingPlanDto.DayDto day)
    {
        ArgumentNullException.ThrowIfNull(day);

        return new(
            day.DayOfWeek,
            day.WeekNumber,
            day.Notes,
            day.Exercises.Select(MyDayExerciseResponse.From).ToArray());
    }
}

/// <summary>Plano de treino ativo do cliente.</summary>
public sealed record MyTrainingPlanResponse(
    Guid Id,
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate,
    IReadOnlyList<MyTrainingDayResponse> Days,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o plano do DTO de portal.</summary>
    public static MyTrainingPlanResponse From(MyTrainingPlanDto plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.TrainingModality,
            plan.Notes,
            plan.StartDate,
            plan.EndDate,
            plan.Days.Select(MyTrainingDayResponse.From).ToArray(),
            plan.UpdatedAt);
    }
}

/// <summary>Totais nutricionais apresentados ao cliente.</summary>
public sealed record MyNutritionTotalsResponse(
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatsGrams,
    decimal Kcal,
    decimal FiberGrams)
{
    /// <summary>Projeta os totais do DTO de portal.</summary>
    public static MyNutritionTotalsResponse From(MyNutritionPlanDto.TotalsDto totals)
    {
        ArgumentNullException.ThrowIfNull(totals);

        return new(
            totals.ProteinGrams,
            totals.CarbsGrams,
            totals.FatsGrams,
            totals.Kcal,
            totals.FiberGrams);
    }
}

/// <summary>Alimento prescrito, com marcador de indisponibilidade.</summary>
public sealed record MyMealItemResponse(
    int OrderNumber,
    string FoodName,
    bool IsUnavailable,
    decimal QuantityInGrams,
    MyNutritionTotalsResponse Contribution)
{
    /// <summary>Projeta o item do DTO de portal.</summary>
    public static MyMealItemResponse From(MyNutritionPlanDto.ItemDto item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new(
            item.OrderNumber,
            item.FoodName,
            item.IsUnavailable,
            item.QuantityInGrams,
            MyNutritionTotalsResponse.From(item.Contribution));
    }
}

/// <summary>Suplemento associado a uma refeição.</summary>
public sealed record MyMealSupplementResponse(
    int OrderNumber,
    string SupplementName,
    bool IsUnavailable,
    string UnitOfMeasure,
    decimal Quantity,
    string? Notes)
{
    /// <summary>Projeta o suplemento do DTO de portal.</summary>
    public static MyMealSupplementResponse From(MyNutritionPlanDto.SupplementDto supplement)
    {
        ArgumentNullException.ThrowIfNull(supplement);

        return new(
            supplement.OrderNumber,
            supplement.SupplementName,
            supplement.IsUnavailable,
            supplement.UnitOfMeasure,
            supplement.Quantity,
            supplement.Notes);
    }
}

/// <summary>Refeição prescrita e os seus totais.</summary>
public sealed record MyMealResponse(
    string MealType,
    int OrderNumber,
    MyNutritionTotalsResponse Totals,
    IReadOnlyList<MyMealItemResponse> Items,
    IReadOnlyList<MyMealSupplementResponse> Supplements)
{
    /// <summary>Projeta a refeição do DTO de portal.</summary>
    public static MyMealResponse From(MyNutritionPlanDto.MealDto meal)
    {
        ArgumentNullException.ThrowIfNull(meal);

        return new(
            meal.MealType,
            meal.OrderNumber,
            MyNutritionTotalsResponse.From(meal.Totals),
            meal.Items.Select(MyMealItemResponse.From).ToArray(),
            meal.Supplements.Select(MyMealSupplementResponse.From).ToArray());
    }
}

/// <summary>Plano alimentar ativo do cliente.</summary>
public sealed record MyNutritionPlanResponse(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartsDate,
    DateOnly? EndsDate,
    decimal TargetKcal,
    decimal ProteinTargetGrams,
    decimal CarbsTargetGrams,
    decimal FatsTargetGrams,
    MyNutritionTotalsResponse ActualTotals,
    IReadOnlyList<MyMealResponse> Meals,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o plano do DTO de portal.</summary>
    public static MyNutritionPlanResponse From(MyNutritionPlanDto plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.StartsDate,
            plan.EndsDate,
            plan.TargetKcal,
            plan.ProteinTargetGrams,
            plan.CarbsTargetGrams,
            plan.FatsTargetGrams,
            MyNutritionTotalsResponse.From(plan.ActualTotals),
            plan.Meals.Select(MyMealResponse.From).ToArray(),
            plan.UpdatedAt);
    }
}

/// <summary>Campos de contacto que o cliente pode alterar.</summary>
public sealed record UpdateMyProfileRequest(
    string? ContactEmail,
    string Phone,
    string? EmergencyContactName,
    string? EmergencyContactPhone);

/// <summary>Perfil do cliente autenticado.</summary>
public sealed record MyProfileResponse(
    string Name,
    string? ContactEmail,
    string Phone,
    DateOnly BirthDate,
    string Sex,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? AvatarUrl,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o perfil do DTO de portal.</summary>
    public static MyProfileResponse From(MyProfileDto profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new(
            profile.Name,
            profile.ContactEmail,
            profile.Phone,
            profile.BirthDate,
            profile.Sex,
            profile.EmergencyContactName,
            profile.EmergencyContactPhone,
            profile.AvatarUrl,
            profile.UpdatedAt);
    }
}

/// <summary>Check-in do próprio cliente.</summary>
public sealed record MyCheckInResponse(
    Guid Id,
    DateOnly CheckInDate,
    DateOnly? TargetDate,
    decimal? WeightKg,
    decimal? BodyFatPercentage,
    string? Notes,
    BodyMeasurementsPayload BodyMeasurements,
    CheckInFeedbackPayload Feedback,
    int? TrainingAdherenceScore,
    int? NutritionAdherenceScore,
    string Status,
    DateTime? RespondedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o check-in, omitindo os campos internos do trainer.</summary>
    public static MyCheckInResponse From(CheckInDto checkIn)
    {
        ArgumentNullException.ThrowIfNull(checkIn);

        return new(
            checkIn.Id,
            checkIn.CheckInDate,
            checkIn.TargetDate,
            checkIn.WeightKg,
            checkIn.BodyFatPercentage,
            checkIn.Notes,
            BodyMeasurementsPayload.From(checkIn.BodyMeasurements),
            CheckInFeedbackPayload.From(checkIn.Feedback),
            checkIn.TrainingAdherenceScore,
            checkIn.NutritionAdherenceScore,
            checkIn.Status,
            checkIn.RespondedAt,
            checkIn.UpdatedAt);
    }
}

/// <summary>Suplemento atribuído, tal como o cliente o vê.</summary>
public sealed record MySupplementAssignmentResponse(
    Guid Id,
    Guid SupplementId,
    string SupplementName,
    string? SupplementDescription,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes,
    bool IsSupplementArchived,
    DateTime UpdatedAt)
{
    /// <summary>Projeta a atribuição do DTO de portal.</summary>
    public static MySupplementAssignmentResponse From(MySupplementAssignmentDto assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return new(
            assignment.Id,
            assignment.SupplementId,
            assignment.SupplementName,
            assignment.SupplementDescription,
            assignment.UnitOfMeasure,
            assignment.ServingSize,
            assignment.Timing,
            assignment.TrainerNotes,
            assignment.IsSupplementArchived,
            assignment.UpdatedAt);
    }
}
