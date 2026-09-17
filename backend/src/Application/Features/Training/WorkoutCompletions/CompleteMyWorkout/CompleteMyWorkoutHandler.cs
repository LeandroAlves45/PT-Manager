using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.WorkoutCompletions.Abstractions;
using Application.Features.Training.WorkoutCompletions.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.WorkoutCompletions.CompleteMyWorkout;

/// <summary>
/// Marca o treino do dia como concluído pelo cliente autenticado. É idempotente e não
/// mexe em sessões nem em packs.
/// </summary>
public sealed class CompleteMyWorkoutHandler
{
    private readonly IValidator<CompleteMyWorkoutCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IWorkoutCompletionStore _store;

    public CompleteMyWorkoutHandler(
        IValidator<CompleteMyWorkoutCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IWorkoutCompletionStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<MyWorkoutCompletionDto>> HandleAsync(
        CompleteMyWorkoutCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<MyWorkoutCompletionDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireClient(
            _tenantContext, TrainingErrors.TrainingClientOnly);
        if (!actor.IsSuccess)
            return Result<MyWorkoutCompletionDto>.Failure(actor.Error!);

        var outcome = await _store.CompleteAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            command.TrainingPlanDayId,
            command.Notes,
            DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc),
            cancellationToken);

        return outcome.Kind switch
        {
            WorkoutCompletionStoreResult.Status.Completed or
            WorkoutCompletionStoreResult.Status.AlreadyCompleted =>
                Result<MyWorkoutCompletionDto>.Success(new MyWorkoutCompletionDto(
                    outcome.Completion!.Id,
                    outcome.Completion.TrainingPlanId,
                    outcome.Completion.TrainingPlanDayId,
                    outcome.Completion.LocalDate,
                    outcome.Completion.Notes,
                    outcome.Completion.CompletedAt)),
            WorkoutCompletionStoreResult.Status.NotFound =>
                Result<MyWorkoutCompletionDto>.Failure(
                    TrainingErrors.StructureReferenceNotFound),
            WorkoutCompletionStoreResult.Status.TrainingPlanInactive =>
                Result<MyWorkoutCompletionDto>.Failure(TrainingErrors.TrainingPlanInactive),
            WorkoutCompletionStoreResult.Status.DateOutsidePlan =>
                Result<MyWorkoutCompletionDto>.Failure(TrainingErrors.TrainingDateOutsidePlan),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }
}
