using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Results;

namespace Application.Features.Training.Exercises.GetExercise;

/// <summary>Obtém um exercício global ou privado visível ao personal trainer.</summary>
public sealed class GetExerciseHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IExerciseQueries _exerciseQueries;

    public GetExerciseHandler(
        ITenantContext tenantContext,
        IExerciseQueries exerciseQueries
    )
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(exerciseQueries);
        _tenantContext = tenantContext;
        _exerciseQueries = exerciseQueries;
    }

    /// <summary>Devolve detalhe ou NotFound seguro.</summary>
    public async Task<Result<ExerciseDto>> HandleAsync(
        GetExerciseQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.ExerciseId == Guid.Empty)
        {
            return Result<ExerciseDto>.Failure(Error.Validation([
                new ValidationError(
                    "ExerciseId",
                    "exercise_id_required",
                    "Exercise ID is required."
                )
            ]));
        }

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<ExerciseDto>.Failure(tenant.Error!);

        var exercise = await _exerciseQueries.GetAsync(
            query.ExerciseId,
            cancellationToken
        );

        return exercise is null
            ? Result<ExerciseDto>.Failure(TrainingErrors.ExerciseNotFound)
            : Result<ExerciseDto>.Success(exercise);
    }
}
