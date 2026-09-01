using Application.Errors;

namespace Application.Features.Administration.ContentModeration;

/// <summary>Erros estáveis dos casos administrativos de moderação privada.</summary>
public static class ContentModerationErrors
{
    public static readonly Error AdministratorOnly = Error.Create(
        "content_moderation_administrator_only",
        ErrorCategory.Forbidden,
        "Only an active superuser in administrative context can moderate private catalog content.");

    public static readonly Error ResourceNotFound = Error.Create(
        "content_moderation_resource_not_found",
        ErrorCategory.NotFound,
        "The private catalog resource was not found.");

    public static readonly Error ResourceNotPrivate = Error.Create(
        "content_moderation_resource_not_private",
        ErrorCategory.Conflict,
        "Only private catalog resources can be moderated by this operation.");
}
