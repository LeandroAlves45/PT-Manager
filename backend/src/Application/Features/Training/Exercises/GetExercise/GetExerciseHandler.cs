using Application.Common.Abstractions;
using Application.Common.Authorization;
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
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _exerciseQueries = exerciseQueries ?? throw new ArgumentNullException(nameof(exerciseQueries));
    }

    /// <summary>Devolve detalhe ou NotFound seguro.</summary>
    public async Task<Result<ExerciseDto>> HandleAsync(
        GetExerciseQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.ExerciseId == Guid.Empty)
            return Result<ExerciseDto>.Failure(TrainingErrors.ExerciseIdRequired());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, TrainingErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<ExerciseDto>.Failure(actor.Error!);

        var exercise = await _exerciseQueries.GetAsync(
            query.ExerciseId,
            cancellationToken
        );

        return exercise is null
            ? Result<ExerciseDto>.Failure(TrainingErrors.ExerciseNotFound)
            : Result<ExerciseDto>.Success(exercise);
    }
}
