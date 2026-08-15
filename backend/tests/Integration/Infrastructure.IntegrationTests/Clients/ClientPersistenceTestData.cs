using Domain.Entities.Billing;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;

namespace Infrastructure.IntegrationTests.Clients;

/// <summary>Cria grafos válidos sem contornar as invariantes do Domain.</summary>
internal static class ClientPersistenceTestData
{
    internal static readonly DateTime NowUtc = new(
        2026,
        8,
        8,
        12,
        0,
        0,
        DateTimeKind.Utc);

    internal static User CreateTrainer(string discriminator)
    {
        var trainer = new User(
            new EmailAddress($"trainer-{discriminator}@example.test"),
            "trainer",
            $"Trainer {discriminator}",
            NowUtc);
        trainer.SetPasswordHash("integration-test-password-hash", NowUtc);
        return trainer;
    }

    internal static Client CreateClient(
        Guid trainerId,
        string discriminator,
        bool isActive = true,
        string? name = null,
        string? contactEmail = null,
        string? phone = null,
        DateTime? now = null)
    {
        var effectiveNow = now ?? NowUtc;
        var client = new Client(
            trainerId,
            name ?? $"Client {discriminator}",
            contactEmail ?? $"client-{discriminator}@example.test",
            phone ?? $"+3519{Guid.NewGuid():N}"[..17],
            BirthDate.Create(
                new DateOnly(1995, 1, 1),
                DateOnly.FromDateTime(effectiveNow)),
            BiologicalSex.Female,
            "Strength",
            null,
            null,
            null,
            effectiveNow);

        if (!isActive)
            client.Deactivate(effectiveNow.AddMinutes(1));

        return client;
    }

    internal static TrainerSubscription CreateSubscription(
        Guid trainerId,
        SubscriptionStatus status,
        int clientLimit,
        int currentClientCount,
        bool isExemptFromBilling = false)
    {
        var subscription = new TrainerSubscription(
            trainerId,
            NowUtc.AddDays(15),
            NowUtc);
        subscription.ChangeTier(SubscriptionTier.Free, clientLimit, NowUtc);
        subscription.SetBillingExemption(isExemptFromBilling, NowUtc);

        for (var count = 0; count < currentClientCount; count++)
            subscription.RegisterClientAdded(NowUtc);

        if (status == SubscriptionStatus.Inactive)
            subscription.Deactivate(NowUtc);
        else if (status == SubscriptionStatus.Suspended)
            subscription.Suspend(NowUtc);
        else if (status == SubscriptionStatus.Cancelled)
            subscription.Cancel(NowUtc);
        else if (status != SubscriptionStatus.Active)
            throw new ArgumentOutOfRangeException(nameof(status));

        return subscription;
    }

    internal static PackType CreatePackType(
        Guid trainerId,
        string discriminator,
        int sessionCount = 10)
    {
        return new PackType(
            trainerId,
            $"Pack {discriminator}",
            sessionCount,
            10000,
            "EUR",
            expectedDurationDays: null,
            NowUtc);
    }

    internal static ClientSessionPack CreatePack(
        Guid trainerId,
        Guid clientId,
        PackType packType,
        DateOnly purchaseDate,
        DateOnly? expectedEndDate,
        int sessionsToConsume = 0,
        DateTime? now = null)
    {
        var effectiveNow = now ?? NowUtc;
        var pack = new ClientSessionPack(
            trainerId,
            clientId,
            packType,
            purchaseDate,
            expectedEndDate,
            effectiveNow);

        for (var count = 0; count < sessionsToConsume; count++)
            pack.ConsumeSession(effectiveNow.AddMinutes(count + 1));

        return pack;
    }

    internal static async Task PersistAsync(
        PostgresContainerFixture fixture,
        Guid trainerId,
        params object[] entities)
    {
        await using var context = fixture.CreateContext(trainerId);
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }
}
