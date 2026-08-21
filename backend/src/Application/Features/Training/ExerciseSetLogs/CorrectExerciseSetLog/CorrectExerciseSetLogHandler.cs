using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.ExerciseSetLogs.CorrectExerciseSetLog;

/// <summary>Corrige valores de um log sem alterar a sua identidade.</summary>
public sealed class CorrectExerciseSetLogHandler
{
    private readonly IValidator<CorrectExerciseSetLogCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IExerciseSetLogStore _store;
    private readonly IExerciseSetLogQueries _queries;

    public CorrectExerciseSetLogHandler(
        IValidator<CorrectExerciseSetLogCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IExerciseSetLogStore store,
        IExerciseSetLogQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<ClientExerciseSetLogDto>> HandleAsync(
        CorrectExerciseSetLogCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ClientExerciseSetLogDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, TrainingErrors.ExerciseSetLogTrainerOnly);
        if (!actor.IsSuccess)
            return Result<ClientExerciseSetLogDto>.Failure(actor.Error!);

        var now = DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc);
        var outcome = await _store.CorrectAsync(
            actor.Value.TrainerId,
            new CorrectExerciseSetLogWriteModel(
                command.ExerciseSetLogId,
                command.WeightKg,
                command.RepsDone,
                command.Notes,
                command.PerformedAt),
            new DateTimeOffset(now),
            now,
            cancellationToken);

        if (outcome.Kind == ExerciseSetLogStoreResult.Status.Corrected)
        {
            var dto = await _queries.GetAsync(outcome.Log!.Id, cancellationToken);
            return Result<ClientExerciseSetLogDto>.Success(dto
                ?? throw new InvalidOperationException(
                    "A corrected exercise set log must be readable."));
        }

        return outcome.ToCorrectionFailure();
    }
}
