using Application.Common.Abstractions;
using Domain.Entities.Sessions;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.TrainerSettings;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.TrainerSettings;

[Collection(PostgresCollection.Name)]
public sealed class TrainerSettingsPersistenceTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public TrainerSettingsPersistenceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetRequired_WhenTrainerSettingsMissing_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        await context.TrainerSettings
            .Where(settings => settings.TrainerId == tenant.TrainerId)
            .ExecuteDeleteAsync(cancellationToken);
        var store = new Infrastructure.Persistence.TrainerSettings.TrainerSettingsStore(context);

        var action = () => store.UpdateBrandingAsync(
            tenant.TrainerId, "Studio Fit", null, null, Now, cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task ReplaceLogo_WritesSettingsAndOutboxInSameTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var store = new Infrastructure.Persistence.TrainerSettings.TrainerSettingsStore(context);
        await store.ReplaceLogoAsync(
            tenant.TrainerId, "https://cdn/logo-1.png", "logo-1",
            Guid.NewGuid(), Now, cancellationToken);

        var outcome = await store.ReplaceLogoAsync(
            tenant.TrainerId, "https://cdn/logo-2.png", "logo-2",
            Guid.NewGuid(), Now.AddMinutes(1), cancellationToken);

        Assert.Equal("logo-1", outcome.PreviousLogoPublicId);
        var outboxCount = await context.OutboxMessages
            .CountAsync(
                message => message.TrainerId == tenant.TrainerId &&
                    message.MessageType == "trainer-logo.delete",
                cancellationToken);
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task ReplaceLogo_UsesFixedCorrelationIdempotencyKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var store = new Infrastructure.Persistence.TrainerSettings.TrainerSettingsStore(context);
        await store.ReplaceLogoAsync(
            tenant.TrainerId, "https://cdn/logo-1.png", "logo-1",
            Guid.NewGuid(), Now, cancellationToken);
        var correlationId = Guid.NewGuid();

        await store.ReplaceLogoAsync(
            tenant.TrainerId, "https://cdn/logo-2.png", "logo-2",
            correlationId, Now.AddMinutes(1), cancellationToken);

        var duplicateKeyCount = await context.OutboxMessages
            .CountAsync(
                message => message.IdempotencyKey ==
                    $"trainer-logo.delete:{correlationId:N}",
                cancellationToken);
        Assert.Equal(1, duplicateKeyCount);
    }

    [Fact]
    public async Task ReplaceLogo_WhenSameAssetIsReplayed_DoesNotScheduleItsDeletion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var store = new Infrastructure.Persistence.TrainerSettings.TrainerSettingsStore(context);
        await store.ReplaceLogoAsync(
            tenant.TrainerId,
            "https://cdn/logo-1.png",
            "logo-1",
            Guid.NewGuid(),
            Now,
            cancellationToken);
        var correlationId = Guid.NewGuid();
        await store.ReplaceLogoAsync(
            tenant.TrainerId,
            "https://cdn/logo-2.png",
            "logo-2",
            correlationId,
            Now.AddMinutes(1),
            cancellationToken);

        var replay = await store.ReplaceLogoAsync(
            tenant.TrainerId,
            "https://cdn/logo-2.png",
            "logo-2",
            correlationId,
            Now.AddMinutes(2),
            cancellationToken);

        Assert.Null(replay.PreviousLogoPublicId);
        var payloads = await context.OutboxMessages
            .Where(message => message.TrainerId == tenant.TrainerId)
            .Select(message => message.Payload)
            .ToListAsync(cancellationToken);
        Assert.DoesNotContain(
            payloads,
            payload => payload.Contains("logo-2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChangeTimezone_ToSameValue_IsIdempotentAndSkipsConflictCheck()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var store = new Infrastructure.Persistence.TrainerSettings.TrainerSettingsStore(context);

        var outcome = await store.ChangeTimezoneAsync(
            tenant.TrainerId, "Europe/Lisbon", Now, cancellationToken);

        Assert.Equal(Application.Features.TrainerSettings.Abstractions
            .TrainerSettingsStoreResult.Status.Updated, outcome.Kind);
    }

    [Fact]
    public async Task ChangeTimezone_WhenCreatesTwoScheduledSessionsOnSameLocalDay_ReturnsConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using (var seed = _fixture.CreateContext(tenant.TrainerId))
        {
            // Duas sessões que só coincidem no mesmo dia local depois da
            // mudança de timezone (23:00 UTC e 03:00 UTC do dia seguinte
            // colapsam no mesmo dia local em UTC-4).
            seed.Sessions.Add(new Session(
                tenant.TrainerId, tenant.ClientId, null,
                new DateTimeOffset(2026, 9, 1, 23, 0, 0, TimeSpan.Zero),
                60, null, null, null, Now));
            seed.Sessions.Add(new Session(
                tenant.TrainerId, tenant.ClientId, null,
                new DateTimeOffset(2026, 9, 2, 3, 0, 0, TimeSpan.Zero),
                60, null, null, null, Now));
            await seed.SaveChangesAsync(cancellationToken);
        }
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var store = new Infrastructure.Persistence.TrainerSettings.TrainerSettingsStore(context);

        var outcome = await store.ChangeTimezoneAsync(
            tenant.TrainerId, "America/New_York", Now.AddMinutes(1), cancellationToken);

        Assert.Equal(Application.Features.TrainerSettings.Abstractions
            .TrainerSettingsStoreResult.Status.ScheduleConflict, outcome.Kind);
    }
}
