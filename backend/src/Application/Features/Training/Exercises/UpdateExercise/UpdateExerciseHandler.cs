using Application.Common.Abstractions;
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
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(exerciseStore);
        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _exerciseStore = exerciseStore;
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

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<ExerciseDto>.Failure(tenant.Error!);

        var outcome = await _exerciseStore.UpdateAsync(
            command.ExerciseId,
            tenant.Value,
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
