using Application.Common.Abstractions;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Results;
using Application.Validation;
using Domain.Entities.Training;
using FluentValidation;

namespace Application.Features.Training.Exercises.CreateExercise;

/// <summary>Cria um exercício pertecente ao tenant autenticado.</summary>
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
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(exerciseStore);
        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _exerciseStore = exerciseStore;
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

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<ExerciseDto>.Failure(tenant.Error!);

        var exercise = new Exercise(
            tenant.Value,
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
