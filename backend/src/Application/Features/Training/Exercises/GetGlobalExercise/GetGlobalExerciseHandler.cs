using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Results;

namespace Application.Features.Training.Exercises.GetGlobalExercise;

/// <summary>Obtém exclusivamente um exercício global.</summary>
public sealed class GetGlobalExerciseHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IGlobalExerciseQueries _queries;

    public GetGlobalExerciseHandler(
        ITenantContext tenantContext,
        IGlobalExerciseQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<GlobalExerciseDto>> HandleAsync(
        GetGlobalExerciseQuery query,
        CancellationToken cancellationToken)
    {
        if (query.ExerciseId == Guid.Empty)
            return Result<GlobalExerciseDto>.Failure(TrainingErrors.ExerciseIdRequired());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext, TrainingErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<GlobalExerciseDto>.Failure(actor.Error!);

        var exercise = await _queries.GetAsync(query.ExerciseId, cancellationToken);

        return exercise is null
            ? Result<GlobalExerciseDto>.Failure(TrainingErrors.ExerciseNotFound)
            : Result<GlobalExerciseDto>.Success(exercise);
    }
}
