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
