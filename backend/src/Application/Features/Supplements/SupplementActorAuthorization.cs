using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Results;

namespace Application.Features.Supplements;

/// <summary>
/// Valida os três atores suportados pelo módulo de suplementos delegando na
/// autorização comum e preservando os códigos de erro da feature.
/// </summary>
internal static class SupplementActorAuthorization
{
    internal static Result<ActorAuthorization.TrainerActor> RequireTrainer(
        ITenantContext tenantContext) =>
        ActorAuthorization.RequireTrainer(tenantContext, SupplementErrors.TrainerOnly);

    internal static Result<ActorAuthorization.ClientActor> RequireClient(
        ITenantContext tenantContext) =>
        ActorAuthorization.RequireClient(tenantContext, SupplementErrors.ClientOnly);

    internal static Result<ActorAuthorization.AdministratorActor> RequireAdministrator(
        ITenantContext tenantContext) =>
        ActorAuthorization.RequireAdministrator(tenantContext, SupplementErrors.AdministratorOnly);
}
