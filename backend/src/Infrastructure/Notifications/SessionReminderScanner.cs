using System.Text.Json;
using Application.Common.Abstractions;
using Application.Common.Time;
using Application.Features.Notifications.Abstractions;
using Application.Features.Notifications.Delivery;
using Application.Features.Notifications.SessionReminders;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Notifications;

/// <summary>
/// Põe na fila os lembretes da véspera. Para cada personal trainer ativo calcula amanhã no seu
/// fuso e, por cada sessão marcada nesse dia de um cliente ativo, enfileira um email ao
/// cliente (se a ficha tiver email de contacto) e outro ao personal trainer. A chave de cada
/// email é a sessão, o dia e o destinatário: repetir o scan não duplica, e uma sessão mudada
/// para outro dia futuro gera a sua própria véspera. O envio volta a confirmar a sessão.
/// </summary>
internal sealed class SessionReminderScanner : ISessionReminderScanner
{
    private const string NotificationType = "session_reminder";

    private static readonly JsonSerializerOptions TemplateDataOptions = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger<SessionReminderScanner> _logger;

    public SessionReminderScanner(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        ILogger<SessionReminderScanner> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SessionReminderScanResult> ScanAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var trainers = await LoadTrainersAsync(cancellationToken);

        var sessions = 0;
        var queued = 0;
        var alreadyQueued = 0;
        var withoutEmail = 0;

        foreach (var trainer in trainers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(trainer.Timezone);
            }
            catch (Exception exception) when (
                exception is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                _logger.LogWarning(
                    "Session reminder scan skipped a personal trainer with an invalid timezone.");
                continue;
            }

            var tomorrow = LocalDates.Today(now, timeZone).AddDays(1);
            var range = LocalDates.ToUtcRange(tomorrow, tomorrow.AddDays(1), timeZone);

            await using var scope = _scopeFactory.CreateAsyncScope();
            scope.ServiceProvider
                .GetRequiredService<ITenantContextInitializer>()
                .Establish(trainer.Id, userId: null, role: null, TenantOrigin.Job, isAdministrative: false);
            var dbContext = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
            var queue = scope.ServiceProvider.GetRequiredService<INotificationQueueStore>();

            var tomorrowSessions = await (
                    from session in dbContext.Sessions.AsNoTracking()
                    join client in dbContext.Clients.AsNoTracking()
                        on session.ClientId equals client.Id
                    where session.OwnerTrainerId == trainer.Id &&
                        client.OwnerTrainerId == trainer.Id &&
                        client.IsActive &&
                        !session.IsDeleted &&
                        session.Status == SessionStatus.Scheduled &&
                        session.StartsAt >= range.StartUtc &&
                        session.StartsAt < range.EndUtc
                    orderby session.StartsAt, session.Id
                    select new
                    {
                        session.Id,
                        session.StartsAt,
                        session.ClientId,
                        ClientName = client.Name,
                        client.ContactEmail
                    })
                .ToListAsync(cancellationToken);

            foreach (var session in tomorrowSessions)
            {
                sessions++;
                var data = JsonSerializer.Serialize(
                    new SessionReminderTemplateData(
                        session.Id,
                        tomorrow,
                        session.ClientName,
                        trainer.Name,
                        session.StartsAt.ToUniversalTime(),
                        trainer.Timezone),
                    TemplateDataOptions);

                if (string.IsNullOrWhiteSpace(session.ContactEmail))
                    withoutEmail++;
                else
                    Count(await EnqueueAsync(
                        queue, trainer.Id, session.ClientId, session.ContactEmail,
                        SessionReminderTemplates.Client, data,
                        OperationKey(session.Id, tomorrow, "client"), now, cancellationToken));

                Count(await EnqueueAsync(
                    queue, trainer.Id, clientId: null, trainer.Email,
                    SessionReminderTemplates.Trainer, data,
                    OperationKey(session.Id, tomorrow, "trainer"), now, cancellationToken));
            }
        }

        return new SessionReminderScanResult(sessions, queued, alreadyQueued, withoutEmail);

        void Count(NotificationQueueStoreStatus status)
        {
            if (status == NotificationQueueStoreStatus.Queued)
                queued++;
            else if (status == NotificationQueueStoreStatus.AlreadyQueued)
                alreadyQueued++;
        }
    }

    private async Task<IReadOnlyList<ScanTrainer>> LoadTrainersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>()
            .Establish(null, userId: null, role: null, TenantOrigin.System, isAdministrative: false);
        var dbContext = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        // IgnoreQueryFilters é explícito: sem tenant, a leitura percorre os personal trainers
        // ativos e só traz o que o email precisa.
        return await (
                from user in dbContext.Users.IgnoreQueryFilters().AsNoTracking()
                join settings in dbContext.TrainerSettings.IgnoreQueryFilters().AsNoTracking()
                    on user.Id equals settings.TrainerId
                // Durante o bloqueio não sai a véspera: o personal trainer não pode cancelar a sessão.
                where user.Role == "trainer" && user.IsActive && !user.IsDeleted &&
                    !dbContext.TrainerAccessBlocks.Any(block => block.TrainerId == user.Id)
                orderby user.Id
                select new ScanTrainer(
                    user.Id,
                    user.FullName ?? settings.AppName,
                    user.Email,
                    settings.Timezone))
            .ToListAsync(cancellationToken);
    }

    private static async Task<NotificationQueueStoreStatus> EnqueueAsync(
        INotificationQueueStore queue,
        Guid trainerId,
        Guid? clientId,
        string recipientEmail,
        string templateKey,
        string templateDataJson,
        string operationKey,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var result = await queue.EnqueueAsync(
            new NotificationQueueRequest(
                trainerId,
                clientId,
                recipientEmail,
                NotificationType,
                templateKey,
                templateDataJson,
                operationKey,
                Guid.NewGuid(),
                now,
                now),
            cancellationToken);

        return result.Kind;
    }

    private static string OperationKey(Guid sessionId, DateOnly localDate, string recipient) =>
        $"session-reminder:{sessionId:N}:{localDate:yyyyMMdd}:{recipient}";

    private sealed record ScanTrainer(Guid Id, string Name, string Email, string Timezone);
}
