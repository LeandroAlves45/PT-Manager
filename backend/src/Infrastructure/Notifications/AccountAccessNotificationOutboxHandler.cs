using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common.Abstractions;
using Application.Features.Jobs.Dispatching;
using Domain.Entities.Administration;
using Domain.Entities.Jobs;
using Infrastructure.Data;
using Infrastructure.Email;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Notifications;

/// <summary>Grava no outbox os emails do bloqueio de um personal trainer, um por destinatário.</summary>
internal static class AccountAccessNotifications
{
    internal const string MessageType = "account_access_notification";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>
    /// Uma mensagem por destinatário; o identificador do evento (a entrada de auditoria do
    /// bloqueio ou do levantamento) torna a chave única e serve de correlação.
    /// </summary>
    internal static OutboxMessage Create(
        Guid trainerId,
        string kind,
        Guid recipientUserId,
        Guid eventId,
        DateTime now) =>
        new(
            trainerId,
            MessageType,
            JsonSerializer.Serialize(new AccountAccessPayload(kind, recipientUserId), JsonOptions),
            $"account-access:{eventId:N}:{recipientUserId:N}",
            eventId,
            now);
}

internal sealed record AccountAccessPayload(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("recipient_user_id")] Guid RecipientUserId);

/// <summary>
/// Envia um email de bloqueio ou de acesso reposto. Tudo o que o email diz é lido no envio:
/// se o bloqueio já foi levantado (ou voltou), se o cliente foi arquivado entretanto ou se o
/// prazo já acabou, o email deixa de fazer sentido e termina sem envio.
/// </summary>
internal sealed class AccountAccessNotificationOutboxHandler : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly PtManagerDbContext _dbContext;
    private readonly ITrainerTimeZoneProvider _timeZones;
    private readonly IClock _clock;
    private readonly IRenderedEmailSender _sender;

    public AccountAccessNotificationOutboxHandler(
        PtManagerDbContext dbContext,
        ITrainerTimeZoneProvider timeZones,
        IClock clock,
        IRenderedEmailSender sender)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeZones = timeZones ?? throw new ArgumentNullException(nameof(timeZones));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public string MessageType => AccountAccessNotifications.MessageType;

    /// <summary>O bloqueio não mexe na subscrição; o email sai seja qual for o estado dela.</summary>
    public bool RequiresActiveSubscription => false;

    public async Task<DispatchItemOutcome> HandleAsync(
        OutboxMessageEnvelope message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        AccountAccessPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<AccountAccessPayload>(message.Payload, JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null || message.TrainerId is not { } trainerId ||
            payload.RecipientUserId == Guid.Empty ||
            !AccountAccessTemplateRenderer.IsSupported(payload.Kind))
            return DispatchItemOutcome.PermanentFailure("account_access_payload_invalid");

        var forTrainer = payload.Kind is AccountAccessTemplateRenderer.TrainerBlockedKind
            or AccountAccessTemplateRenderer.TrainerRestoredKind;
        if (forTrainer && payload.RecipientUserId != trainerId)
            return DispatchItemOutcome.PermanentFailure("account_access_payload_invalid");

        var now = _clock.UtcNow;
        var block = await _dbContext.TrainerAccessBlocks
            .AsNoTracking()
            .Where(item => item.TrainerId == trainerId)
            .Select(item => new { Reason = item.Reason.Value, item.Note, item.BlockedAt })
            .FirstOrDefaultAsync(cancellationToken);

        var aboutBlock = payload.Kind is AccountAccessTemplateRenderer.TrainerBlockedKind
            or AccountAccessTemplateRenderer.ClientBlockedKind;
        if (aboutBlock != (block is not null))
            return DispatchItemOutcome.Succeeded();

        var trainer = await _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.Id == trainerId && user.Role == "trainer" && user.IsActive && !user.IsDeleted)
            .Select(user => new { user.Email, user.FullName })
            .FirstOrDefaultAsync(cancellationToken);
        if (trainer is null)
            return DispatchItemOutcome.Succeeded();

        string recipientEmail;
        string? recipientName;
        if (forTrainer)
        {
            recipientEmail = trainer.Email;
            recipientName = trainer.FullName;
        }
        else
        {
            var client = await ActiveClientAccounts(trainerId)
                .Where(item => item.UserId == payload.RecipientUserId)
                .FirstOrDefaultAsync(cancellationToken);
            // Arquivado entretanto: não recebe nem ganha os 7 dias.
            if (client is null)
                return DispatchItemOutcome.Succeeded();
            recipientEmail = client.Email;
            recipientName = client.FullName;
        }

        DateTime? endsAtLocal = null;
        string? timezoneId = null;
        if (block is not null && !TrainerAccessBlock.ClientAccessEnded(block.BlockedAt, now))
        {
            var clientsCanEnter = !forTrainer ||
                await ActiveClientAccounts(trainerId).AnyAsync(cancellationToken);
            if (clientsCanEnter)
            {
                var timezone = await _timeZones.GetRequiredAsync(trainerId, cancellationToken);
                endsAtLocal = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(block.BlockedAt.AddDays(TrainerAccessBlock.ClientGraceDays), DateTimeKind.Utc),
                    timezone);
                timezoneId = timezone.Id;
            }
        }

        // O prazo do cliente já acabou: o aviso de que vai acabar chegaria tarde.
        if (payload.Kind == AccountAccessTemplateRenderer.ClientBlockedKind && endsAtLocal is null)
            return DispatchItemOutcome.Succeeded();

        var rendered = AccountAccessTemplateRenderer.Render(new AccountAccessEmail(
            payload.Kind,
            recipientName,
            trainer.FullName,
            forTrainer ? block?.Reason : null,
            forTrainer ? block?.Note : null,
            endsAtLocal,
            timezoneId));

        var outcome = await _sender.SendAsync(recipientEmail, rendered, message.IdempotencyKey, cancellationToken);
        return outcome.Kind switch
        {
            ResendTransportOutcomeKind.Sent => DispatchItemOutcome.Succeeded(),
            ResendTransportOutcomeKind.TransientFailure => DispatchItemOutcome.TransientFailure(outcome.FailureCode!),
            _ => DispatchItemOutcome.PermanentFailure(outcome.FailureCode!)
        };
    }

    // Clientes ativos com conta ativa: os únicos que entram no portal durante os 7 dias.
    private IQueryable<ClientAccount> ActiveClientAccounts(Guid trainerId) =>
        from client in _dbContext.Clients.AsNoTracking()
        join user in _dbContext.Users.IgnoreQueryFilters().AsNoTracking() on client.UserId equals (Guid?)user.Id
        where client.OwnerTrainerId == trainerId && client.IsActive && !client.IsDeleted &&
            user.Role == "client" && user.IsActive && !user.IsDeleted
        select new ClientAccount { UserId = user.Id, Email = user.Email, FullName = client.Name };

    private sealed class ClientAccount
    {
        public Guid UserId { get; init; }
        public string Email { get; init; } = null!;
        public string? FullName { get; init; }
    }
}
