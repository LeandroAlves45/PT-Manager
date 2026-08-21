using Application.Common.Abstractions;
using Application.Errors;
using Application.Results;

namespace Application.Common.Authorization;

/// <summary>
/// Valida os três atores suportados pela plataforma de forma fail-closed.
/// Cada feature fornece o seu erro funcional de role para preservar códigos
/// estáveis já publicados.
/// </summary>
internal static class ActorAuthorization
{
    /// <summary>Exige tenant resolvido, role "trainer" e utilizador autenticado.</summary>
    internal static Result<TrainerActor> RequireTrainer(
        ITenantContext tenantContext,
        Error roleError
    )
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(roleError);

        var tenant = tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<TrainerActor>.Failure(tenant.Error!);

        if (!string.Equals(tenantContext.Role, "trainer", StringComparison.Ordinal))
            return Result<TrainerActor>.Failure(roleError);
        if (!tenantContext.UserId.HasValue || tenantContext.UserId.Value == Guid.Empty)
            return Result<TrainerActor>.Failure(CommonErrors.UnauthenticatedUser);

        return Result<TrainerActor>.Success(
            new TrainerActor(tenant.Value, tenantContext.UserId.Value));
    }

    /// <summary>Exige tenant resolvido, role "client" e utilizador autenticado.</summary>
    internal static Result<ClientActor> RequireClient(
        ITenantContext tenantContext,
        Error roleError
    )
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(roleError);

        var tenant = tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<ClientActor>.Failure(tenant.Error!);

        if (!string.Equals(tenantContext.Role, "client", StringComparison.Ordinal))
            return Result<ClientActor>.Failure(roleError);
        if (!tenantContext.UserId.HasValue || tenantContext.UserId.Value == Guid.Empty)
            return Result<ClientActor>.Failure(CommonErrors.UnauthenticatedUser);

        return Result<ClientActor>.Success(
            new ClientActor(tenant.Value, tenantContext.UserId.Value));
    }

    /// <summary>
    /// Exige cumulativamente role "superuser", contexto administrativo e utilizador autenticado.
    /// Não exige tenant: a administração global opera fora de qualquer tenant.
    /// </summary>
    internal static Result<AdministratorActor> RequireAdministrator(
        ITenantContext tenantContext,
        Error roleError
    )
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(roleError);

        if (!string.Equals(tenantContext.Role, "superuser", StringComparison.Ordinal) ||
            !tenantContext.IsAdministrative)
            return Result<AdministratorActor>.Failure(roleError);

        if (!tenantContext.UserId.HasValue || tenantContext.UserId.Value == Guid.Empty)
            return Result<AdministratorActor>.Failure(CommonErrors.UnauthenticatedUser);

        return Result<AdministratorActor>.Success(
            new AdministratorActor(tenantContext.UserId.Value));
    }

    /// <summary>Ator personal trainer autenticado do tenant efetivo.</summary>
    internal sealed record TrainerActor(Guid TrainerId, Guid UserId);

    /// <summary>Ator cliente autenticado do tenant efetivo.</summary>
    internal sealed record ClientActor(Guid TrainerId, Guid UserId);

    /// <summary>Ator superuser autenticado em contexto administrativo.</summary>
    internal sealed record AdministratorActor(Guid UserId);
}
