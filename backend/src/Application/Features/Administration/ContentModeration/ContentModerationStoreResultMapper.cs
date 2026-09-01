using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Results;

namespace Application.Features.Administration.ContentModeration;

/// <summary>Mapeia resultados de persistência para resultados de aplicação.</summary>
internal static class ContentModerationStoreResultMapper
{
    internal static Result ToResult(this PrivateCatalogModerationStoreResult outcome) => outcome switch
    {
        PrivateCatalogModerationStoreResult.Changed or
        PrivateCatalogModerationStoreResult.AlreadyInRequestedState => Result.Success(),
        PrivateCatalogModerationStoreResult.NotFound =>
            Result.Failure(ContentModerationErrors.ResourceNotFound),
        PrivateCatalogModerationStoreResult.NotPrivate =>
            Result.Failure(ContentModerationErrors.ResourceNotPrivate),
        PrivateCatalogModerationStoreResult.ActorInvalid =>
            Result.Failure(ContentModerationErrors.AdministratorOnly),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };
}
