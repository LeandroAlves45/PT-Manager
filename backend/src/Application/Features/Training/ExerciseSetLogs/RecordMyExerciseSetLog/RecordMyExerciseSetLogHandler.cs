using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.ExerciseSetLogs.RecordMyExerciseSetLog;

/// <summary>
/// Regista uma série do cliente autenticado no seu plano ativo. Não
/// reutiliza o caso de uso do personal trainer: a identidade do cliente vem só do token.
/// </summary>
public sealed class RecordMyExerciseSetLogHandler
{
    private readonly IValidator<RecordMyExerciseSetLogCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMyExerciseSetLogStore _store;

    public RecordMyExerciseSetLogHandler(
        IValidator<RecordMyExerciseSetLogCommand> validator,
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
        RecordMyExerciseSetLogCommand command,
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

        var outcome = await _store.RecordAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            command.TrainingPlanDayExerciseId,
            command.SetNumber,
            command.WeightKg,
            command.RepsDone,
            command.Rpe,
            command.Notes,
            DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc),
            cancellationToken);

        // Prescrição inexistente ou de outro cliente responde como referência não encontrada.
        return outcome.ToDtoResult(TrainingErrors.StructureReferenceNotFound);
    }

}
