using Application.Common;
using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.Exercises.ListExercises;

/// <summary>Lista exercícios globais e privados visíveis ao personal trainer.</summary>
public sealed class ListExercisesHandler
{
    private readonly IValidator<ListExercisesQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IExerciseQueries _exerciseQueries;

    public ListExercisesHandler(
        IValidator<ListExercisesQuery> validator,
        ITenantContext tenantContext,
        IExerciseQueries exerciseQueries
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _exerciseQueries = exerciseQueries ?? throw new ArgumentNullException(nameof(exerciseQueries));
    }

    /// <summary>Devolve uma página determinística sem materializar entidades.</summary>
    public async Task<Result<PageResult<ExerciseDto>>> HandleAsync(
        ListExercisesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<ExerciseDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, TrainingErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<ExerciseDto>>.Failure(actor.Error!);

        var page = await _exerciseQueries.ListAsync(
            SearchTerm.Normalize(query.Search),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken
        );

        return Result<PageResult<ExerciseDto>>.Success(page);
    }
}
