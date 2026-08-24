using Application.Features.Clients.Abstractions;
using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Clients;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Clients;

[Collection(PostgresCollection.Name)]
public sealed class ClientActiveRelationshipTests
{
    private readonly PostgresContainerFixture _fixture;
    private readonly ClientStoreTestContextFactory _contextFactory;

    public ClientActiveRelationshipTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
        _contextFactory = new ClientStoreTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task ArchivedRelationship_AllowsSameUserInAnotherTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var trainerA = ClientPersistenceTestData.CreateTrainer("history-a");
        var trainerB = ClientPersistenceTestData.CreateTrainer("history-b");
        var user = CreateClientUser("shared-client");
        var oldClient = ClientPersistenceTestData.CreateClient(
            trainerA.Id,
            "old",
            isActive: false);
        var newClient = ClientPersistenceTestData.CreateClient(trainerB.Id, "new");

        oldClient.AttachUser(user.Id, ClientPersistenceTestData.NowUtc.AddMinutes(2));
        newClient.AttachUser(user.Id, ClientPersistenceTestData.NowUtc.AddMinutes(2));

        await using (var tenantA = _fixture.CreateContext(trainerA.Id))
        {
            tenantA.AddRange(trainerA, user, oldClient);
            await tenantA.SaveChangesAsync(cancellationToken);
        }

        await using (var tenantB = _fixture.CreateContext(trainerB.Id))
        {
            tenantB.AddRange(trainerB, newClient);
            await tenantB.SaveChangesAsync(cancellationToken);
        }

        await using var verification = _fixture.CreateAdministrativeContext();
        Assert.Equal(2, await verification.Clients
            .IgnoreQueryFilters()
            .CountAsync(client => client.UserId == user.Id, cancellationToken));
    }

    [Fact]
    public async Task SecondActiveRelationship_IsRejectedByNamedConstraint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var trainerA = ClientPersistenceTestData.CreateTrainer("active-a");
        var trainerB = ClientPersistenceTestData.CreateTrainer("active-b");
        var user = CreateClientUser("active-user");
        var clientA = ClientPersistenceTestData.CreateClient(trainerA.Id, "active-a");
        var clientB = ClientPersistenceTestData.CreateClient(trainerB.Id, "active-b");
        clientA.AttachUser(user.Id, ClientPersistenceTestData.NowUtc.AddMinutes(1));
        clientB.AttachUser(user.Id, ClientPersistenceTestData.NowUtc.AddMinutes(1));

        await using (var tenantA = _fixture.CreateContext(trainerA.Id))
        {
            tenantA.AddRange(trainerA, user, clientA);
            await tenantA.SaveChangesAsync(cancellationToken);
        }

        await using var tenantB = _fixture.CreateContext(trainerB.Id);
        tenantB.AddRange(trainerB, clientB);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => tenantB.SaveChangesAsync(cancellationToken));
        var postgres = FindPostgresException(exception);

        Assert.Equal("uq_clients_user_active", postgres.ConstraintName);
    }

    [Fact]
    public async Task Reactivate_WhenAnotherTenantOwnsActiveRelationship_ReturnsSafeConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedConflictingRelationshipsAsync(cancellationToken);
        await using var context = _fixture.CreateContext(seed.TrainerAId);
        var store = new ClientStore(
            context,
            new PostgresConstraintTranslator());

        var outcome = await store.ReactivateAsync(
            seed.ArchivedClientId,
            seed.TrainerAId,
            ClientPersistenceTestData.NowUtc.AddHours(1),
            cancellationToken);

        Assert.Equal(
            ReactivateClientStoreOutcome.UserAlreadyHasActiveRelationship,
            outcome);
        context.ChangeTracker.Clear();
        Assert.False((await context.Clients.SingleAsync(
            client => client.Id == seed.ArchivedClientId,
            cancellationToken)).IsActive);
    }

    [Fact]
    public async Task Reactivate_TwoWorkersDifferentTenants_OnlyOneWins()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedTwoArchivedRelationshipsAsync(cancellationToken);
        using var barrier = new Barrier(participantCount: 3);

        var workerA = RunReactivateWorkerAsync(
            seed.TrainerAId,
            seed.ClientAId,
            barrier,
            cancellationToken);
        var workerB = RunReactivateWorkerAsync(
            seed.TrainerBId,
            seed.ClientBId,
            barrier,
            cancellationToken);
        barrier.SignalAndWait(cancellationToken);

        var outcomes = await Task.WhenAll(workerA, workerB);

        Assert.Contains(ReactivateClientStoreOutcome.Reactivated, outcomes);
        Assert.Contains(ReactivateClientStoreOutcome.UserAlreadyHasActiveRelationship, outcomes);

        await using var verification = _fixture.CreateAdministrativeContext();
        var activeCount = await verification.Clients
            .IgnoreQueryFilters()
            .CountAsync(
                client => client.UserId == seed.UserId && client.IsActive,
                cancellationToken);
        Assert.Equal(1, activeCount);
    }

    private Task<ReactivateClientStoreOutcome> RunReactivateWorkerAsync(
        Guid trainerId,
        Guid clientId,
        Barrier barrier,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            await using var context = _contextFactory.Create(trainerId);
            var store = new ClientStore(context, new PostgresConstraintTranslator());
            barrier.SignalAndWait(cancellationToken);
            return await store.ReactivateAsync(
                clientId,
                trainerId,
                ClientPersistenceTestData.NowUtc.AddHours(1),
                cancellationToken);
        }, cancellationToken);
    }

    private async Task<TwoTenantSeed> SeedTwoArchivedRelationshipsAsync(
        CancellationToken cancellationToken)
    {
        var trainerA = ClientPersistenceTestData.CreateTrainer("concurrent-a");
        var trainerB = ClientPersistenceTestData.CreateTrainer("concurrent-b");
        var user = CreateClientUser("concurrent-user");
        var clientA = ClientPersistenceTestData.CreateClient(
            trainerA.Id,
            "concurrent-a",
            isActive: false);
        var clientB = ClientPersistenceTestData.CreateClient(
            trainerB.Id,
            "concurrent-b",
            isActive: false);
        clientA.AttachUser(user.Id, ClientPersistenceTestData.NowUtc.AddMinutes(2));
        clientB.AttachUser(user.Id, ClientPersistenceTestData.NowUtc.AddMinutes(2));
        var subscriptionA = ClientPersistenceTestData.CreateSubscription(
            trainerA.Id,
            SubscriptionStatus.Active,
            clientLimit: 5,
            currentClientCount: 0);
        var subscriptionB = ClientPersistenceTestData.CreateSubscription(
            trainerB.Id,
            SubscriptionStatus.Active,
            clientLimit: 5,
            currentClientCount: 0);

        await using (var tenantA = _fixture.CreateContext(trainerA.Id))
        {
            tenantA.AddRange(trainerA, user, clientA, subscriptionA);
            await tenantA.SaveChangesAsync(cancellationToken);
        }

        await using (var tenantB = _fixture.CreateContext(trainerB.Id))
        {
            tenantB.AddRange(trainerB, clientB, subscriptionB);
            await tenantB.SaveChangesAsync(cancellationToken);
        }

        return new TwoTenantSeed(trainerA.Id, clientA.Id, trainerB.Id, clientB.Id, user.Id);
    }

    private async Task<Seed> SeedConflictingRelationshipsAsync(
        CancellationToken cancellationToken)
    {
        var trainerA = ClientPersistenceTestData.CreateTrainer("reactivate-a");
        var trainerB = ClientPersistenceTestData.CreateTrainer("reactivate-b");
        var user = CreateClientUser("reactivate-user");
        var archived = ClientPersistenceTestData.CreateClient(
            trainerA.Id,
            "reactivate-old",
            isActive: false);
        var active = ClientPersistenceTestData.CreateClient(trainerB.Id, "reactivate-new");
        archived.AttachUser(user.Id, ClientPersistenceTestData.NowUtc.AddMinutes(2));
        active.AttachUser(user.Id, ClientPersistenceTestData.NowUtc.AddMinutes(2));
        var subscription = ClientPersistenceTestData.CreateSubscription(
            trainerA.Id,
            SubscriptionStatus.Active,
            clientLimit: 5,
            currentClientCount: 0);

        await using (var tenantA = _fixture.CreateContext(trainerA.Id))
        {
            tenantA.AddRange(trainerA, user, archived, subscription);
            await tenantA.SaveChangesAsync(cancellationToken);
        }

        await using (var tenantB = _fixture.CreateContext(trainerB.Id))
        {
            tenantB.AddRange(trainerB, active);
            await tenantB.SaveChangesAsync(cancellationToken);
        }

        return new Seed(trainerA.Id, archived.Id);
    }

    private static Npgsql.PostgresException FindPostgresException(Exception exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is Npgsql.PostgresException postgres)
                return postgres;
            current = current.InnerException;
        }

        throw new InvalidOperationException("Expected PostgreSQL exception.");
    }

    private static User CreateClientUser(string discriminator)
    {
        var user = new User(
            new EmailAddress($"{discriminator}@example.test"),
            "client",
            $"Client {discriminator}",
            ClientPersistenceTestData.NowUtc);
        user.SetPasswordHash(
            "integration-test-password-hash",
            ClientPersistenceTestData.NowUtc);
        return user;
    }

    private sealed record Seed(Guid TrainerAId, Guid ArchivedClientId);

    private sealed record TwoTenantSeed(
        Guid TrainerAId,
        Guid ClientAId,
        Guid TrainerBId,
        Guid ClientBId,
        Guid UserId);
}
