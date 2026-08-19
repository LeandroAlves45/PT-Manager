using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;

namespace Application.Features.Supplements.GetMySupplementAssignment;

/// <summary>Obtém uma prescrição ativa do cliente autenticado.</summary>
public sealed class GetMySupplementAssignmentHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClientSupplementAssignmentQueries _queries;

    public GetMySupplementAssignmentHandler(
        ITenantContext tenantContext,
        IClientSupplementAssignmentQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<MySupplementAssignmentDto>> HandleAsync(
        GetMySupplementAssignmentQuery query,
        CancellationToken cancellationToken)
    {
        if (query.AssignmentId == Guid.Empty)
            return Result<MySupplementAssignmentDto>.Failure(
                SupplementErrors.AssignmentIdRequired);

        var actor = SupplementActorAuthorization.RequireClient(_tenantContext);
        if (!actor.IsSuccess)
            return Result<MySupplementAssignmentDto>.Failure(actor.Error!);

        var assignment = await _queries.GetMyAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            query.AssignmentId,
            cancellationToken);

        return assignment is null
            ? Result<MySupplementAssignmentDto>.Failure(SupplementErrors.AssignmentNotFound)
            : Result<MySupplementAssignmentDto>.Success(assignment);
    }
}
