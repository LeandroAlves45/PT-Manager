using Application.Errors;

namespace Application.Features.ClientPortal;

/// <summary>Erros estáveis do portal do cliente.</summary>
public static class ClientPortalErrors
{
    public static readonly Error ClientOnly = Error.Create(
        "portal_client_only",
        ErrorCategory.Forbidden,
        "Only an authenticated client can access the portal.");

    public static readonly Error TrainingPlanNotAvailable = Error.Create(
        "portal_training_plan_not_available",
        ErrorCategory.NotFound,
        "No training plan is available.");

    /// <summary>
    /// Data do filtro de dia que não é um dia passado do plano ativo (hoje, futuro, fora do plano
    /// ou em falta). Sai como erro de validação do campo <c>date</c>.
    /// </summary>
    public static readonly Error WorkoutDayNotAvailable = Error.Validation([
        new ValidationError(
            "Date",
            "portal_workout_day_not_available",
            "Only a past day of the active training plan can be opened.")
    ]);

    public static readonly Error NutritionPlanNotAvailable = Error.Create(
        "portal_nutrition_plan_not_available",
        ErrorCategory.NotFound,
        "No nutrition plan is available.");

    public static readonly Error NutritionPlanNotSelected = Error.Create(
        "portal_nutrition_plan_not_selected",
        ErrorCategory.NotFound,
        "No nutrition plan has been chosen yet.");

    public static readonly Error MealPlanNotFound = Error.Create(
        "portal_meal_plan_not_found",
        ErrorCategory.NotFound,
        "The nutrition plan is not available.");

    public static readonly Error PlanSupplementNotFound = Error.Create(
        "portal_plan_supplement_not_found",
        ErrorCategory.NotFound,
        "The supplement is not part of the meal plan the client follows.");

    public static readonly Error PlanSupplementUnavailable = Error.Create(
        "portal_plan_supplement_unavailable",
        ErrorCategory.Conflict,
        "The supplement is no longer available and its intake cannot be recorded.");

    public static readonly Error ProfileNotAvailable = Error.Create(
        "portal_profile_not_available",
        ErrorCategory.NotFound,
        "The profile is not available.");

    public static readonly Error ProfilePhoneAlreadyExists = Error.Create(
        "client_phone_already_exists",
        ErrorCategory.Conflict,
        "A client with this phone already exists.");

    /// <summary>
    /// Password em falta, errada, ou conta bloqueada por tentativas falhadas: a mesma resposta
    /// para os três, sem revelar qual. Sai como erro do campo <c>password</c> (400), e não como
    /// 401, para o cliente da API não o confundir com uma sessão expirada.
    /// </summary>
    public static readonly Error AccountDeletionPasswordInvalid = Error.Validation([
        new ValidationError(
            "Password",
            "portal_account_deletion_password_invalid",
            "The password is incorrect.")
    ]);

    public static readonly Error AvatarCompensationFailed = Error.Create(
        "portal_avatar_compensation_failed",
        ErrorCategory.Internal,
        "The avatar was uploaded but the profile could not be saved and the uploaded" +
        "asset could not be removed. Manual cleanup is required.");

    public static readonly Error AvatarPersistenceFailed = Error.Create(
        "portal_avatar_persistence_failed",
        ErrorCategory.Internal,
        "The avatar was uploaded but the profile could not be saved. The uploaded asset" +
        "was removed.");
}
