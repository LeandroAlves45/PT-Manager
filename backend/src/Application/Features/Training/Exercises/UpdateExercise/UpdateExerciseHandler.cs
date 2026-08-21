using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.Exercises.UpdateExercise;

/// <summary>Atualiza um exercício privado e rejeita catálogo global.</summary>
public sealed class UpdateExerciseHandler
{
    private readonly IValidator<UpdateExerciseCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IExerciseStore _exerciseStore;

    public UpdateExerciseHandler(
        IValidator<UpdateExerciseCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IExerciseStore exerciseStore
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _exerciseStore = exerciseStore ?? throw new ArgumentNullException(nameof(exerciseStore));
    }

    /// <summary>Valida, atualiza e persiste um exercício privado.</summary>
    public async Task<Result<ExerciseDto>> HandleAsync(
        UpdateExerciseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ExerciseDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, TrainingErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<ExerciseDto>.Failure(actor.Error!);

        var outcome = await _exerciseStore.UpdateAsync(
            command.ExerciseId,
            actor.Value.TrainerId,
            command.Name,
            command.Description,
            command.MuscleGroups,
            command.Equipment,
            command.DifficultyLevel,
            command.VideoUrl,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToUpdateResult();
    }
}
