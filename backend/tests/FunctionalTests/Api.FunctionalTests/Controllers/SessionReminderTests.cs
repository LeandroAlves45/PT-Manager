using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Api.Contracts.Sessions;
using Api.FunctionalTests.Support;
using Application.Features.Jobs.Dispatching;
using Application.Features.Notifications.Delivery;
using Application.Features.Notifications.SessionReminders;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Email da véspera da sessão, do scan ao envio, contra PostgreSQL real. O gateway é trocado
/// por um gravador; o scan, a fila, o dispatcher e a revalidação no envio são os reais. A base
/// é partilhada com a suite, por isso as asserções olham só para os emails deste tenant.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class SessionReminderTests : IDisposable
{
    private readonly PostgresApiFixture _fixture;
    private readonly RecordingGateway _gateway = new();
    private readonly ApiWebApplicationFactory _factory;

    public SessionReminderTests(PostgresApiFixture fixture)
    {
        _fixture = fixture;
        _factory = new ApiWebApplicationFactory(fixture.ConnectionString)
        {
            ConfigureServices = services =>
                services.AddSingleton<INotificationDeliveryGateway>(_gateway)
        };
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TomorrowSession_SendsOneEmailToTheClientAndOneToTheTrainer_OnlyOnce()
    {
        var tenant = await SeedAsync("Europe/Lisbon");
        var sessionId = await SeedSessionAsync(tenant, LocalTomorrowAt(tenant.TimeZone, 10, 0));

        var scan = await ScanAsync();
        await ActivateAsync();
        await ActivateAsync();

        var sent = SentFor(tenant);
        Assert.True(scan.Queued >= 2);
        Assert.Equal(2, sent.Count);
        var toClient = Assert.Single(sent, item => item.Recipient == tenant.ClientEmail);
        var toTrainer = Assert.Single(sent, item => item.Recipient == tenant.TrainerEmail);
        Assert.Equal(SessionReminderTemplates.Client, toClient.TemplateKey);
        Assert.Equal(SessionReminderTemplates.Trainer, toTrainer.TemplateKey);
        Assert.Equal(sessionId, toClient.Data.SessionId);
        Assert.Equal(LocalTomorrow(tenant.TimeZone), toClient.Data.SessionLocalDate);
    }

    [Fact]
    public async Task TodayPastAndDayAfterTomorrow_SendNothing()
    {
        var tenant = await SeedAsync("Europe/Lisbon");
        var today = LocalDateOf(DateTimeOffset.UtcNow, tenant.TimeZone);
        await SeedSessionAsync(tenant, LocalAt(tenant.TimeZone, today, 23, 59));
        await SeedSessionAsync(tenant, LocalAt(tenant.TimeZone, today.AddDays(-1), 10, 0));
        await SeedSessionAsync(tenant, LocalAt(tenant.TimeZone, today.AddDays(2), 10, 0));

        await ActivateAsync();

        Assert.Empty(SentFor(tenant));
    }

    [Fact]
    public async Task TwoSessionsTomorrow_SendTwoEmailsToTheTrainer()
    {
        var tenant = await SeedAsync("Europe/Lisbon");
        var secondClient = await SeedClientAsync(tenant, "segundo");
        await SeedSessionAsync(tenant, LocalTomorrowAt(tenant.TimeZone, 9, 0));
        await SeedSessionAsync(tenant with { ClientId = secondClient }, LocalTomorrowAt(tenant.TimeZone, 11, 0));

        await ActivateAsync();

        Assert.Equal(2, SentFor(tenant).Count(item => item.Recipient == tenant.TrainerEmail));
    }

    [Fact]
    public async Task ClientWithoutEmail_OnlyTheTrainerIsReminded()
    {
        var tenant = await SeedAsync("Europe/Lisbon", clientEmail: null);
        await SeedSessionAsync(tenant, LocalTomorrowAt(tenant.TimeZone, 10, 0));

        var scan = await ScanAsync();
        await ActivateAsync();

        var sent = Assert.Single(SentFor(tenant));
        Assert.Equal(tenant.TrainerEmail, sent.Recipient);
        Assert.True(scan.ClientsWithoutEmail >= 1);
    }

    [Fact]
    public async Task CancelledBeforeSending_IsSkipped()
    {
        var tenant = await SeedAsync("Europe/Lisbon");
        var sessionId = await SeedSessionAsync(tenant, LocalTomorrowAt(tenant.TimeZone, 10, 0));
        await ScanAsync();

        var cancel = await TrainerClient(tenant).PostAsync(
            $"/api/v1/sessions/{sessionId}/cancel-by-trainer", null, Token);
        await ActivateAsync();

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Empty(SentFor(tenant));
        Assert.Equal(["skipped", "skipped"], await NotificationStatusesAsync(tenant));
    }

    [Fact]
    public async Task MovedToAnotherDay_IsSkipped_AndMovingItBackSendsTheNewEve()
    {
        var tenant = await SeedAsync("Europe/Lisbon");
        var tomorrow = LocalTomorrowAt(tenant.TimeZone, 10, 0);
        var sessionId = await SeedSessionAsync(tenant, tomorrow);
        await ScanAsync();
        var client = TrainerClient(tenant);

        await ApiJsonPayload.PatchAsync(
            client, $"/api/v1/sessions/{sessionId}/reschedule",
            new RescheduleSessionRequest(tomorrow.AddDays(3), 60, null), Token);
        await ActivateAsync();
        var afterMove = SentFor(tenant).Count;
        await ApiJsonPayload.PatchAsync(
            client, $"/api/v1/sessions/{sessionId}/reschedule",
            new RescheduleSessionRequest(tomorrow.AddHours(2), 60, null), Token);
        await ActivateAsync();

        Assert.Equal(0, afterMove);
        Assert.Equal(2, SentFor(tenant).Count);
    }

    [Fact]
    public async Task BlockedTrainer_GetsNoEve_AndLiftingTheBlockOnTheEveBringsItBack()
    {
        var tenant = await SeedAsync("Europe/Lisbon");
        await SeedSessionAsync(tenant, LocalTomorrowAt(tenant.TimeZone, 10, 0));
        await BlockTrainerAsync(tenant);

        await ActivateAsync();
        var duringBlock = SentFor(tenant).Count;
        await UnblockTrainerAsync(tenant);
        await ActivateAsync();

        Assert.Equal(0, duringBlock);
        Assert.Equal(2, SentFor(tenant).Count);
    }

    [Fact]
    public async Task BlockedBetweenScanAndSend_IsSkipped_AndTheKeyIsFreedForTheSameEve()
    {
        var tenant = await SeedAsync("Europe/Lisbon");
        await SeedSessionAsync(tenant, LocalTomorrowAt(tenant.TimeZone, 10, 0));
        await ScanAsync();
        await BlockTrainerAsync(tenant);

        await ActivateAsync();
        var statuses = await NotificationStatusesAsync(tenant);
        await UnblockTrainerAsync(tenant);
        await ActivateAsync();

        Assert.Equal(["skipped", "skipped"], statuses);
        Assert.Equal(2, SentFor(tenant).Count);
    }

    [Fact]
    public async Task SuspendedSubscription_StillSendsTheReminder()
    {
        var tenant = await SeedAsync("Europe/Lisbon");
        await SeedSessionAsync(tenant, LocalTomorrowAt(tenant.TimeZone, 10, 0));
        await ExecuteAsync(
            db => db.TrainerSubscriptions.IgnoreQueryFilters()
                .Where(item => item.TrainerId == tenant.TrainerId)
                .ExecuteUpdateAsync(set => set.SetProperty(
                    item => item.Status, Domain.ValueObjects.SubscriptionStatus.Suspended), Token));

        await ActivateAsync();

        Assert.Equal(2, SentFor(tenant).Count);
    }

    [Fact]
    public async Task TomorrowIsTheTrainersTomorrow_NotTheServers()
    {
        // Em Auckland, «amanhã» começa umas 11 a 13 horas antes de em Lisboa.
        var tenant = await SeedAsync("Pacific/Auckland");
        var sessionId = await SeedSessionAsync(tenant, LocalTomorrowAt(tenant.TimeZone, 0, 30));

        await ActivateAsync();

        var sent = SentFor(tenant);
        Assert.Equal(2, sent.Count);
        Assert.All(sent, item => Assert.Equal(sessionId, item.Data.SessionId));
        Assert.All(sent, item => Assert.Equal(LocalTomorrow(tenant.TimeZone), item.Data.SessionLocalDate));
    }

    [Fact]
    public async Task ArchivedClient_IsNotReminded()
    {
        var tenant = await SeedAsync("Europe/Lisbon");
        await SeedSessionAsync(tenant, LocalTomorrowAt(tenant.TimeZone, 10, 0));
        await ExecuteAsync(
            db => db.Clients.IgnoreQueryFilters()
                .Where(item => item.Id == tenant.ClientId)
                .ExecuteUpdateAsync(set => set.SetProperty(item => item.IsActive, false), Token));

        await ActivateAsync();

        Assert.Empty(SentFor(tenant));
    }

    private async Task<SessionReminderScanResult> ScanAsync() =>
        await _factory.Services.GetRequiredService<ISessionReminderScanner>().ScanAsync(Token);

    private async Task ActivateAsync() =>
        await _factory.Services.GetRequiredService<IJobDispatchActivation>().ActivateAsync(Token);

    private List<SentEmail> SentFor(ReminderTenant tenant) =>
        _gateway.Sent
            .Where(item => item.Recipient == tenant.TrainerEmail || item.Recipient == tenant.ClientEmail)
            .ToList();

    private async Task<string[]> NotificationStatusesAsync(ReminderTenant tenant)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        return await db.Notifications.IgnoreQueryFilters()
            .Where(item => item.OwnerTrainerId == tenant.TrainerId)
            .Select(item => item.Status)
            .OrderBy(status => status)
            .ToArrayAsync(Token);
    }

    private async Task BlockTrainerAsync(ReminderTenant tenant)
    {
        var superuserId = await AuthTestData.SeedAccountAsync(
            _factory, $"admin-{Guid.NewGuid():N}@reminder.test", role: "superuser");
        await ExecuteAsync(db =>
        {
            db.TrainerAccessBlocks.Add(new Domain.Entities.Administration.TrainerAccessBlock(
                tenant.TrainerId,
                Domain.ValueObjects.PlatformEnforcementReason.MaliciousContent,
                null,
                superuserId,
                DateTime.UtcNow));
            return db.SaveChangesAsync(Token);
        });
    }

    private Task UnblockTrainerAsync(ReminderTenant tenant) =>
        ExecuteAsync(db => db.TrainerAccessBlocks
            .Where(item => item.TrainerId == tenant.TrainerId)
            .ExecuteDeleteAsync(Token));

    private async Task ExecuteAsync(Func<PtManagerDbContext, Task<int>> update)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await update(scope.ServiceProvider.GetRequiredService<PtManagerDbContext>());
    }

    private async Task<ReminderTenant> SeedAsync(string timeZoneId, string? clientEmail = "")
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "reminder", Token);
        var email = clientEmail == "" ? $"cliente-{Guid.NewGuid():N}@reminder.test" : clientEmail;
        var clientId = await TrainerTenantSeeder.SeedClientAsync(
            _factory, trainer.TrainerId, "Cliente da véspera", Token, contactEmail: email);
        await ExecuteAsync(
            db => db.TrainerSettings.IgnoreQueryFilters()
                .Where(item => item.TrainerId == trainer.TrainerId)
                .ExecuteUpdateAsync(set => set.SetProperty(item => item.Timezone, timeZoneId), Token));

        return new ReminderTenant(
            trainer.TrainerId,
            trainer.Email,
            clientId,
            email,
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
    }

    private Task<Guid> SeedClientAsync(ReminderTenant tenant, string suffix) =>
        TrainerTenantSeeder.SeedClientAsync(
            _factory, tenant.TrainerId, $"Cliente {suffix}", Token,
            contactEmail: $"cliente-{suffix}-{Guid.NewGuid():N}@reminder.test");

    private Task<Guid> SeedSessionAsync(ReminderTenant tenant, DateTimeOffset startsAt) =>
        TrainingTestData.SeedSessionAsync(_factory, tenant.TrainerId, tenant.ClientId, null, startsAt, Token);

    private HttpClient TrainerClient(ReminderTenant tenant) =>
        _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(tenant.TrainerId));

    private static DateOnly LocalDateOf(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);

    private static DateOnly LocalTomorrow(TimeZoneInfo timeZone) =>
        LocalDateOf(DateTimeOffset.UtcNow, timeZone).AddDays(1);

    private static DateTimeOffset LocalTomorrowAt(TimeZoneInfo timeZone, int hour, int minute) =>
        LocalAt(timeZone, LocalTomorrow(timeZone), hour, minute);

    private static DateTimeOffset LocalAt(TimeZoneInfo timeZone, DateOnly date, int hour, int minute)
    {
        var local = date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    private sealed record ReminderTenant(
        Guid TrainerId,
        string TrainerEmail,
        Guid ClientId,
        string? ClientEmail,
        TimeZoneInfo TimeZone);

    private sealed record SentEmail(string Recipient, string TemplateKey, SessionReminderTemplateData Data);

    /// <summary>Grava o que o Resend teria recebido, sem rede.</summary>
    private sealed class RecordingGateway : INotificationDeliveryGateway
    {
        public ConcurrentQueue<SentEmail> Queue { get; } = new();

        public IReadOnlyList<SentEmail> Sent => Queue.ToArray();

        public Task<NotificationDeliveryOutcome> SendAsync(
            NotificationDeliveryMessage message,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var data = JsonSerializer.Deserialize<SessionReminderTemplateData>(message.TemplateDataJson!)!;
            Queue.Enqueue(new SentEmail(message.RecipientEmail, message.TemplateKey, data));
            return Task.FromResult(NotificationDeliveryOutcome.Sent());
        }
    }
}
