using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Administration.Trainers.Abstractions;
using Application.Features.Administration.Trainers.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Administration.Trainers.ListExemptTrainers;

public sealed record ListExemptTrainersQuery(int PageNumber, int PageSize);

public sealed class ListExemptTrainersQueryValidator : AbstractValidator<ListExemptTrainersQuery>
{
    public ListExemptTrainersQueryValidator() =>
        this.ApplyPaginationRules(query => query.PageNumber, query => query.PageSize);
}

/// <summary>Lista só os treinadores isentos (U49); não há lista de todos os treinadores.</summary>
public sealed class ListExemptTrainersHandler
{
    private readonly IValidator<ListExemptTrainersQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ITrainerAdministrationQueries _queries;

    public ListExemptTrainersHandler(
        IValidator<ListExemptTrainersQuery> validator,
        ITenantContext tenantContext,
        ITrainerAdministrationQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<AdminTrainerDto>>> HandleAsync(
        ListExemptTrainersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<AdminTrainerDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            TrainerAdministrationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<AdminTrainerDto>>.Failure(actor.Error!);

        return Result<PageResult<AdminTrainerDto>>.Success(await _queries.ListExemptAsync(
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken));
    }
}
