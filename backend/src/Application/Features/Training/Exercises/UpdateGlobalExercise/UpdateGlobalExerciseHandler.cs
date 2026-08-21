using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.Exercises.UpdateGlobalExercise;

/// <summary>Atualiza um exercício global e grava snapshots de auditoria.</summary>
public sealed class UpdateGlobalExerciseHandler
{
    private readonly IValidator<UpdateGlobalExerciseCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IGlobalExerciseStore _store;

    public UpdateGlobalExerciseHandler(
        IValidator<UpdateGlobalExerciseCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IGlobalExerciseStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<GlobalExerciseDto>> HandleAsync(
        UpdateGlobalExerciseCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<GlobalExerciseDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext, TrainingErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<GlobalExerciseDto>.Failure(actor.Error!);

        var outcome = await _store.UpdateAsync(
            actor.Value.UserId,
            command.ExerciseId,
            command.Name,
            command.Description,
            command.MuscleGroups,
            command.Equipment,
            command.DifficultyLevel,
            command.VideoUrl,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToDtoResult();
    }
}
