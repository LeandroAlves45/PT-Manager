using Application.Common;
using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.Exercises.ListGlobalExercises;

/// <summary>Lista exercícios globais para um superuser autorizado.</summary>
public sealed class ListGlobalExercisesHandler
{
    private readonly IValidator<ListGlobalExercisesQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IGlobalExerciseQueries _queries;

    public ListGlobalExercisesHandler(
        IValidator<ListGlobalExercisesQuery> validator,
        ITenantContext tenantContext,
        IGlobalExerciseQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<GlobalExerciseDto>>> HandleAsync(
        ListGlobalExercisesQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<GlobalExerciseDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext, TrainingErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<GlobalExerciseDto>>.Failure(actor.Error!);

        var page = await _queries.ListAsync(
            SearchTerm.Normalize(query.Search),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return Result<PageResult<GlobalExerciseDto>>.Success(page);
    }
}
