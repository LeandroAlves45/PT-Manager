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

    public static readonly Error NutritionPlanNotAvailable = Error.Create(
        "portal_nutrition_plan_not_available",
        ErrorCategory.NotFound,
        "No nutrition plan is available.");

    public static readonly Error ProfileNotAvailable = Error.Create(
        "portal_profile_not_available",
        ErrorCategory.NotFound,
        "The profile is not available.");

    public static readonly Error ProfileEmailAlreadyExists = Error.Create(
        "client_email_already_exists",
        ErrorCategory.Conflict,
        "A client with this email already exists.");

    public static readonly Error ProfilePhoneAlreadyExists = Error.Create(
        "client_phone_already_exists",
        ErrorCategory.Conflict,
        "A client with this phone already exists.");
}
