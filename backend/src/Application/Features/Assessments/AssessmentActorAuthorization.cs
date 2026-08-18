using Application.Common.Abstractions;
using Application.Errors;
using Application.Results;

namespace Application.Features.Assessments;

/// <summary>Valida o ator antes de executar casos de uso de Assessment.</summary>
internal static class AssessmentActorAuthorization
{
    internal static Result<Guid> RequireTrainer(ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        var tenant = tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return tenant;

        return string.Equals(tenantContext.Role, "trainer", StringComparison.Ordinal)
            ? tenant
            : Result<Guid>.Failure(AssessmentErrors.TrainerOnly);
    }

    internal static Result<ClientActor> RequireClient(ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        var tenant = tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<ClientActor>.Failure(tenant.Error!);

        if (!string.Equals(tenantContext.Role, "client", StringComparison.Ordinal))
            return Result<ClientActor>.Failure(AssessmentErrors.ClientOnly);

        if (!tenantContext.UserId.HasValue || tenantContext.UserId.Value == Guid.Empty)
            return Result<ClientActor>.Failure(CommonErrors.UnauthenticatedUser);

        return Result<ClientActor>.Success(new ClientActor(tenant.Value, tenantContext.UserId.Value));
    }

    internal sealed record ClientActor(Guid TrainerId, Guid UserId);
}
