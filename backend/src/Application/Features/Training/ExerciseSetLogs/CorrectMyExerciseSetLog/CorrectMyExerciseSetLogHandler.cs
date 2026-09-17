using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.ExerciseSetLogs.CorrectMyExerciseSetLog;

/// <summary>Corrige uma série do cliente autenticado registada no dia local de hoje.</summary>
public sealed class CorrectMyExerciseSetLogHandler
{
    private readonly IValidator<CorrectMyExerciseSetLogCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMyExerciseSetLogStore _store;

    public CorrectMyExerciseSetLogHandler(
        IValidator<CorrectMyExerciseSetLogCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IMyExerciseSetLogStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<MyExerciseSetLogDto>> HandleAsync(
        CorrectMyExerciseSetLogCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<MyExerciseSetLogDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireClient(
            _tenantContext,
            TrainingErrors.TrainingClientOnly);
        if (!actor.IsSuccess)
            return Result<MyExerciseSetLogDto>.Failure(actor.Error!);

        var outcome = await _store.CorrectAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            command.ExerciseSetLogId,
            command.WeightKg,
            command.RepsDone,
            command.Rpe,
            command.Notes,
            DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc),
            cancellationToken);

        return outcome.ToDtoResult(TrainingErrors.ExerciseSetLogNotFound);
    }
}
