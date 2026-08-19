using Application.Common.Abstractions;
using Application.Errors;
using Application.Results;

namespace Application.Features.Supplements;

/// <summary>Valida os três atores suportados pelo módulo de suplementos.</summary>
internal sealed class SupplementActorAuthorization
{
    internal static Result<TrainerActor> RequireTrainer(ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        var tenant = tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<TrainerActor>.Failure(tenant.Error!);

        if (!string.Equals(tenantContext.Role, "trainer", StringComparison.Ordinal))
            return Result<TrainerActor>.Failure(SupplementErrors.TrainerOnly);
        if (!tenantContext.UserId.HasValue || tenantContext.UserId.Value == Guid.Empty)
            return Result<TrainerActor>.Failure(CommonErrors.UnauthenticatedUser);

        return Result<TrainerActor>.Success(
            new TrainerActor(tenant.Value, tenantContext.UserId.Value));
    }

    internal static Result<ClientActor> RequireClient(ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        var tenant = tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<ClientActor>.Failure(tenant.Error!);

        if (!string.Equals(tenantContext.Role, "client", StringComparison.Ordinal))
            return Result<ClientActor>.Failure(SupplementErrors.ClientOnly);
        if (!tenantContext.UserId.HasValue || tenantContext.UserId.Value == Guid.Empty)
            return Result<ClientActor>.Failure(CommonErrors.UnauthenticatedUser);

        return Result<ClientActor>.Success(
            new ClientActor(tenant.Value, tenantContext.UserId.Value));
    }

    internal static Result<AdministratorActor> RequireAdministrator(ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (!string.Equals(tenantContext.Role, "superuser", StringComparison.Ordinal) ||
            !tenantContext.IsAdministrative)
            return Result<AdministratorActor>.Failure(SupplementErrors.AdministratorOnly);

        if (!tenantContext.UserId.HasValue || tenantContext.UserId.Value == Guid.Empty)
            return Result<AdministratorActor>.Failure(CommonErrors.UnauthenticatedUser);

        return Result<AdministratorActor>.Success(
            new AdministratorActor(tenantContext.UserId.Value));
    }

    internal sealed record TrainerActor(Guid TrainerId, Guid UserId);
    internal sealed record ClientActor(Guid TrainerId, Guid UserId);
    internal sealed record AdministratorActor(Guid UserId);
}
