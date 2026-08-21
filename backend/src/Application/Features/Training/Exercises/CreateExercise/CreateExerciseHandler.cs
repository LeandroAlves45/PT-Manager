using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Results;
using Application.Validation;
using Domain.Entities.Training;
using FluentValidation;

namespace Application.Features.Training.Exercises.CreateExercise;

/// <summary>Cria um exercício pertencente ao tenant autenticado.</summary>
public sealed class CreateExerciseHandler
{
    private readonly IValidator<CreateExerciseCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IExerciseStore _exerciseStore;

    public CreateExerciseHandler(
        IValidator<CreateExerciseCommand> validator,
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

    /// <summary>Valida, cria e persiste um exercício privado.</summary>
    public async Task<Result<ExerciseDto>> HandleAsync(
        CreateExerciseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ExerciseDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, TrainingErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<ExerciseDto>.Failure(actor.Error!);

        var exercise = new Exercise(
            actor.Value.TrainerId,
            command.Name,
            command.Description,
            command.MuscleGroups,
            command.Equipment,
            command.DifficultyLevel,
            command.VideoUrl,
            _clock.UtcNow
        );

        await _exerciseStore.AddAsync(exercise, cancellationToken);

        return Result<ExerciseDto>.Success(exercise.ToDto());
    }
}
