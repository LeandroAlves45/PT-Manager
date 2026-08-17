using Application.Common.Abstractions;
using Application.Results;

namespace Application.Features.Sessions;

/// <summary>Valida o ator antes de permitir operações de gestão de sessões.</summary>
internal static class SessionActorAuthorization
{
    internal static Result<Guid> RequireTrainer(ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        var tenant = tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return tenant;

        // O TrainerId identifica o tenant mas não prova que o ator é o personal trainer.
        if (!string.Equals(tenantContext.Role, "trainer", StringComparison.Ordinal))
            return Result<Guid>.Failure(SessionErrors.TrainerOnly);

        return tenant;
    }
}
