using Application.Common;
using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Packs.PackTypes.Abstractions;
using Application.Features.Packs.PackTypes.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Packs.PackTypes.ListPackTypes;

/// <summary>Lista tipos de pack do tenant com ordenação determinística.</summary>
public sealed class ListPackTypesHandler
{
    private readonly IValidator<ListPackTypesQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IPackTypeQueries _queries;

    public ListPackTypesHandler(
        IValidator<ListPackTypesQuery> validator,
        ITenantContext tenantContext,
        IPackTypeQueries queries
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<PackTypeDto>>> HandleAsync(
        ListPackTypesQuery query,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<PackTypeDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, PackErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<PackTypeDto>>.Failure(actor.Error!);

        var page = await _queries.ListAsync(
            actor.Value.TrainerId,
            SearchTerm.Normalize(query.Search),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken
        );

        return Result<PageResult<PackTypeDto>>.Success(page);
    }
}
