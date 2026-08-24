using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Notifications.Abstractions;
using Application.Features.Notifications.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Notifications.EnqueueNotification;

/// <summary>Valida o ator e delega o enqueue atómico para a store de notificações.</summary>
public sealed class EnqueueNotificationHandler
{
    private readonly IValidator<EnqueueNotificationCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly INotificationQueueStore _store;

    public EnqueueNotificationHandler(
        IValidator<EnqueueNotificationCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        INotificationQueueStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<QueuedNotificationDto>> HandleAsync(
        EnqueueNotificationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<QueuedNotificationDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, NotificationErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<QueuedNotificationDto>.Failure(actor.Error!);

        var now = _clock.UtcNow;
        var outcome = await _store.EnqueueAsync(
            new NotificationQueueRequest(
                actor.Value.TrainerId,
                command.ClientId,
                command.RecipientEmail.Trim(),
                command.NotificationType.Trim(),
                command.TemplateKey.Trim(),
                command.TemplateDataJson,
                command.OperationKey.Trim(),
                command.CorrelationId,
                command.ScheduledAt?.UtcDateTime ?? now,
                now),
            cancellationToken);

        return outcome.Kind switch
        {
            NotificationQueueStoreStatus.Queued or
            NotificationQueueStoreStatus.AlreadyQueued =>
                Result<QueuedNotificationDto>.Success(
                    new QueuedNotificationDto(
                        outcome.NotificationId!.Value,
                        "pending",
                        outcome.QueuedAt!.Value)),
            NotificationQueueStoreStatus.ClientNotFound =>
                Result<QueuedNotificationDto>.Failure(NotificationErrors.ClientNotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
