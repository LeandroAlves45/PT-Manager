using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Packs.ClientSessionPacks.ListClientSessionPacks;

/// <summary>Lista packs atribuídos visíveis no tenant.</summary>
public sealed class ListClientSessionPacksHandler
{
    private readonly IValidator<ListClientSessionPacksQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClientSessionPackQueries _queries;

    public ListClientSessionPacksHandler(
        IValidator<ListClientSessionPacksQuery> validator,
        ITenantContext tenantContext,
        IClientSessionPackQueries queries
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<ClientSessionPackDto>>> HandleAsync(
        ListClientSessionPacksQuery query,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<ClientSessionPackDto>>.Failure(
                validation.ToApplicationError()
            );

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, PackErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<ClientSessionPackDto>>.Failure(actor.Error!);

        var page = await _queries.ListAsync(
            actor.Value.TrainerId,
            query.ClientId,
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken
        );

        return Result<PageResult<ClientSessionPackDto>>.Success(page);
    }
}
