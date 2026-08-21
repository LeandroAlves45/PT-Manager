using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.ExerciseSetLogs.ListExerciseSetLogs;

/// <summary>Lista execuções reais de um cliente.</summary>
public sealed class ListExerciseSetLogsHandler
{
    private readonly IValidator<ListExerciseSetLogsQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IExerciseSetLogQueries _queries;

    public ListExerciseSetLogsHandler(
        IValidator<ListExerciseSetLogsQuery> validator,
        ITenantContext tenantContext,
        IExerciseSetLogQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<ClientExerciseSetLogDto>>> HandleAsync(
        ListExerciseSetLogsQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<ClientExerciseSetLogDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, TrainingErrors.ExerciseSetLogTrainerOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<ClientExerciseSetLogDto>>.Failure(actor.Error!);

        var page = await _queries.ListAsync(
            query.ClientId,
            query.TrainingPlanId,
            query.PerformedFrom?.ToUniversalTime(),
            query.PerformedTo?.ToUniversalTime(),
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return Result<PageResult<ClientExerciseSetLogDto>>.Success(page);
    }
}
