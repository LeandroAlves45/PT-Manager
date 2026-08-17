using Application.Common.Abstractions;
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
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(queries);
        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _store = store;
        _queries = queries;
    }

    public async Task<Result<ClientExerciseSetLogDto>> HandleAsync(
        CorrectExerciseSetLogCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ClientExerciseSetLogDto>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<ClientExerciseSetLogDto>.Failure(tenant.Error!);

        var now = DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc);
        var outcome = await _store.CorrectAsync(
            tenant.Value,
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
