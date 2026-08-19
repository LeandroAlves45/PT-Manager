using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;

namespace Application.Features.Supplements.GetSupplementAssignment;

/// <summary>Obtém uma atribuição sem expor notas internas do catálogo.</summary>
public sealed class GetSupplementAssignmentHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClientSupplementAssignmentQueries _queries;

    public GetSupplementAssignmentHandler(
        ITenantContext tenantContext,
        IClientSupplementAssignmentQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<ClientSupplementAssignmentDto>> HandleAsync(
        GetSupplementAssignmentQuery query,
        CancellationToken cancellationToken)
    {
        if (query.AssignmentId == Guid.Empty)
            return Result<ClientSupplementAssignmentDto>.Failure(SupplementErrors.AssignmentIdRequired);

        var actor = SupplementActorAuthorization.RequireTrainer(_tenantContext);
        if (!actor.IsSuccess)
            return Result<ClientSupplementAssignmentDto>.Failure(actor.Error!);

        var assignment = await _queries.GetAsync(
            actor.Value.TrainerId,
            query.AssignmentId,
            cancellationToken);

        return assignment is null
            ? Result<ClientSupplementAssignmentDto>.Failure(SupplementErrors.AssignmentNotFound)
            : Result<ClientSupplementAssignmentDto>.Success(assignment);
    }
}
