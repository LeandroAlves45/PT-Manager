using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.WorkoutCompletions.Abstractions;
using Application.Features.Training.WorkoutCompletions.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.WorkoutCompletions.ListWorkoutCompletions;

/// <summary>
/// Lista ao personal trainer as conclusões de treino e as notas do cliente. Só leitura: a
/// conclusão é do cliente e não se anula. Cliente de outro tenant devolve lista vazia,
/// como a lista de séries realizadas.
/// </summary>
public sealed class ListWorkoutCompletionsHandler
{
    private readonly IValidator<ListWorkoutCompletionsQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IWorkoutCompletionQueries _queries;

    public ListWorkoutCompletionsHandler(
        IValidator<ListWorkoutCompletionsQuery> validator,
        ITenantContext tenantContext,
        IWorkoutCompletionQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<WorkoutCompletionDto>>> HandleAsync(
        ListWorkoutCompletionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<WorkoutCompletionDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext, TrainingErrors.ExerciseSetLogTrainerOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<WorkoutCompletionDto>>.Failure(actor.Error!);

        var page = await _queries.ListAsync(
            query.ClientId,
            query.TrainingPlanId,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return Result<PageResult<WorkoutCompletionDto>>.Success(page);
    }
}
