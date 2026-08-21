using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Results;

namespace Application.Features.Assessments;

/// <summary>Valida o ator antes de executar casos de uso de Assessment.</summary>
internal static class AssessmentActorAuthorization
{
    internal static Result<Guid> RequireTrainer(ITenantContext tenantContext)
    {
        var actor = ActorAuthorization.RequireTrainer(tenantContext, AssessmentErrors.TrainerOnly);

        return actor.IsSuccess
            ? Result<Guid>.Success(actor.Value.TrainerId)
            : Result<Guid>.Failure(actor.Error!);
    }

    internal static Result<ClientActor> RequireClient(ITenantContext tenantContext)
    {
        var actor = ActorAuthorization.RequireClient(tenantContext, AssessmentErrors.ClientOnly);

        return actor.IsSuccess
            ? Result<ClientActor>.Success(
                new ClientActor(actor.Value.TrainerId, actor.Value.UserId))
            : Result<ClientActor>.Failure(actor.Error!);
    }

    internal sealed record ClientActor(Guid TrainerId, Guid UserId);
}
