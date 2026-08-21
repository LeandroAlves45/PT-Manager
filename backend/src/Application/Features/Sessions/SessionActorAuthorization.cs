using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Results;

namespace Application.Features.Sessions;

/// <summary>Valida o ator antes de permitir operações de gestão de sessões.</summary>
internal static class SessionActorAuthorization
{
    internal static Result<Guid> RequireTrainer(ITenantContext tenantContext)
    {
        var actor = ActorAuthorization.RequireTrainer(tenantContext, SessionErrors.TrainerOnly);

        return actor.IsSuccess
            ? Result<Guid>.Success(actor.Value.TrainerId)
            : Result<Guid>.Failure(actor.Error!);
    }
}
