namespace Application.Features.Administration.ContentModeration.Abstractions;

/// <summary>Resultado esperado de uma transição de enforcement administrativo.</summary>
public enum PrivateCatalogModerationStoreResult
{
    Changed,
    AlreadyInRequestedState,
    NotFound,
    NotPrivate,
    ActorInvalid
}
