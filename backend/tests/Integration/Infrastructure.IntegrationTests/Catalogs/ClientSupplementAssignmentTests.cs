using Application.Features.Supplements.Abstractions;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Errors;
using Infrastructure.Persistence.Supplements;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Catalogs;

[Collection(PostgresCollection.Name)]
public sealed class ClientSupplementAssignmentTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public ClientSupplementAssignmentTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Assign_WhenInstructionsAreOmitted_CopiesCatalogWithoutTrainerNotes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(
            tenant.TrainerId, Now, tenant.TrainerId);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.Supplements.Add(supplement);
        await context.SaveChangesAsync(cancellationToken);
        var store = new ClientSupplementAssignmentStore(
            context, new PostgresConstraintTranslator());

        var outcome = await store.AssignAsync(
            tenant.TrainerId, tenant.ClientId, supplement.Id,
            null, null, null, Now.AddMinutes(1), cancellationToken);

        Assert.Equal(ClientSupplementAssignmentStoreResult.Status.Assigned, outcome.Kind);
        Assert.Equal((supplement.ServingSize, supplement.Timing, null),
            (outcome.Assignment!.ServingSize, outcome.Assignment.Timing,
                outcome.Assignment.TrainerNotes));
    }

    [Fact]
    public async Task UpdateInstructions_ValidChange_PersistsAfterContextIsDiscarded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(
            tenant.TrainerId, Now, tenant.TrainerId);
        Guid assignmentId;
        await using (var context = _fixture.CreateContext(tenant.TrainerId))
        {
            context.Supplements.Add(supplement);
            await context.SaveChangesAsync(cancellationToken);
            var store = new ClientSupplementAssignmentStore(
                context, new PostgresConstraintTranslator());
            var assigned = await store.AssignAsync(
                tenant.TrainerId, tenant.ClientId, supplement.Id,
                null, null, null, Now, cancellationToken);
            assignmentId = assigned.Assignment!.Id;

            var update = await store.UpdateInstructionsAsync(
                tenant.TrainerId, assignmentId,
                "3 g", "evening", "after training",
                Now.AddMinutes(1), cancellationToken);

            Assert.Equal(ClientSupplementAssignmentStoreResult.Status.Updated, update.Kind);
        }

        await using var verify = _fixture.CreateContext(tenant.TrainerId);
        var persisted = await verify.ClientSupplementAssignments
            .AsNoTracking()
            .SingleAsync(item => item.Id == assignmentId, cancellationToken);

        Assert.Equal(
            ("3 g", "evening", "after training"),
            (persisted.ServingSize, persisted.Timing, persisted.TrainerNotes));
    }

    [Fact]
    public async Task Assign_WhenInactiveAssignmentExists_ReturnsConflictOutcome()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(
            tenant.TrainerId, Now, tenant.TrainerId);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.Supplements.Add(supplement);
        await context.SaveChangesAsync(cancellationToken);
        var store = new ClientSupplementAssignmentStore(
            context, new PostgresConstraintTranslator());
        var first = await store.AssignAsync(
            tenant.TrainerId, tenant.ClientId, supplement.Id,
            null, null, null, Now, cancellationToken);
        await store.SetActiveAsync(
            tenant.TrainerId, first.Assignment!.Id, false,
            Now.AddMinutes(1), cancellationToken);

        var second = await store.AssignAsync(
            tenant.TrainerId, tenant.ClientId, supplement.Id,
            null, null, null, Now.AddMinutes(2), cancellationToken);

        Assert.Equal(
            ClientSupplementAssignmentStoreResult.Status.AssignmentAlreadyExists,
            second.Kind);
    }

    [Fact]
    public async Task ArchivedClient_BlocksUpdateAndReactivateButAllowsDeactivate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(
            tenant.TrainerId, Now, tenant.TrainerId);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.Supplements.Add(supplement);
        await context.SaveChangesAsync(cancellationToken);
        var store = new ClientSupplementAssignmentStore(
            context, new PostgresConstraintTranslator());
        var assigned = await store.AssignAsync(
            tenant.TrainerId, tenant.ClientId, supplement.Id,
            null, null, null, Now, cancellationToken);
        var client = await context.Clients.SingleAsync(
            item => item.Id == tenant.ClientId, cancellationToken);
        client.Deactivate(Now.AddMinutes(1));
        await context.SaveChangesAsync(cancellationToken);

        var update = await store.UpdateInstructionsAsync(
            tenant.TrainerId, assigned.Assignment!.Id,
            "3 g", "evening", null, Now.AddMinutes(2), cancellationToken);
        var deactivate = await store.SetActiveAsync(
            tenant.TrainerId, assigned.Assignment.Id, false,
            Now.AddMinutes(2), cancellationToken);
        var reactivate = await store.SetActiveAsync(
            tenant.TrainerId, assigned.Assignment.Id, true,
            Now.AddMinutes(3), cancellationToken);

        Assert.Equal(ClientSupplementAssignmentStoreResult.Status.ClientInactive, update.Kind);
        Assert.Equal(ClientSupplementAssignmentStoreResult.Status.Changed, deactivate.Kind);
        Assert.Equal(ClientSupplementAssignmentStoreResult.Status.ClientInactive, reactivate.Kind);
    }

    [Fact]
    public async Task UpdateInstructions_WhenSupplementIsArchived_ReturnsSupplementInactive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(
            tenant.TrainerId, Now, tenant.TrainerId);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.Supplements.Add(supplement);
        await context.SaveChangesAsync(cancellationToken);
        var store = new ClientSupplementAssignmentStore(
            context, new PostgresConstraintTranslator());
        var assigned = await store.AssignAsync(
            tenant.TrainerId, tenant.ClientId, supplement.Id,
            null, null, null, Now, cancellationToken);
        supplement.Archive(Now.AddMinutes(1));
        await context.SaveChangesAsync(cancellationToken);

        var update = await store.UpdateInstructionsAsync(
            tenant.TrainerId, assigned.Assignment!.Id,
            "3 g", "evening", null, Now.AddMinutes(2), cancellationToken);

        Assert.Equal(ClientSupplementAssignmentStoreResult.Status.SupplementInactive, update.Kind);
    }

    [Fact]
    public async Task Assign_WhenSupplementBelongsToAnotherTenant_ReturnsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = await _fixture.SeedTenantWithClientAsync(
            $"owner-{Guid.NewGuid():N}", cancellationToken);
        var requester = await _fixture.SeedTenantWithClientAsync(
            $"requester-{Guid.NewGuid():N}", cancellationToken);
        var supplement = IntegrationTestData.Supplement(
            owner.TrainerId, Now, owner.TrainerId);
        await using (var ownerContext = _fixture.CreateContext(owner.TrainerId))
        {
            ownerContext.Supplements.Add(supplement);
            await ownerContext.SaveChangesAsync(cancellationToken);
        }
        await using var context = _fixture.CreateContext(requester.TrainerId);
        var store = new ClientSupplementAssignmentStore(
            context, new PostgresConstraintTranslator());

        var outcome = await store.AssignAsync(
            requester.TrainerId, requester.ClientId, supplement.Id,
            null, null, null, Now, cancellationToken);

        Assert.Equal(
            ClientSupplementAssignmentStoreResult.Status.SupplementNotFound,
            outcome.Kind);
    }

    [Fact]
    public async Task Assign_WhenTwoRequestsRace_OneSucceedsAndOneReturnsConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(
            tenant.TrainerId, Now, tenant.TrainerId);
        await using (var seed = _fixture.CreateContext(tenant.TrainerId))
        {
            seed.Supplements.Add(supplement);
            await seed.SaveChangesAsync(cancellationToken);
        }
        await using var firstContext = _fixture.CreateContext(tenant.TrainerId);
        await using var secondContext = _fixture.CreateContext(tenant.TrainerId);
        var firstStore = new ClientSupplementAssignmentStore(
            firstContext, new PostgresConstraintTranslator());
        var secondStore = new ClientSupplementAssignmentStore(
            secondContext, new PostgresConstraintTranslator());

        var outcomes = await Task.WhenAll(
            firstStore.AssignAsync(
                tenant.TrainerId, tenant.ClientId, supplement.Id,
                null, null, null, Now, cancellationToken),
            secondStore.AssignAsync(
                tenant.TrainerId, tenant.ClientId, supplement.Id,
                null, null, null, Now, cancellationToken));

        Assert.Equal(1, outcomes.Count(outcome =>
            outcome.Kind == ClientSupplementAssignmentStoreResult.Status.Assigned));
        Assert.Equal(1, outcomes.Count(outcome =>
            outcome.Kind == ClientSupplementAssignmentStoreResult.Status.AssignmentAlreadyExists));
    }

    [Fact]
    public async Task Database_WhenTwoActiveAssignmentsRace_EnforcesUniqueConstraint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(
            tenant.TrainerId, Now, tenant.TrainerId);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.Supplements.Add(supplement);
        context.ClientSupplementAssignments.AddRange(
            Assignment(tenant, supplement.Id), Assignment(tenant, supplement.Id));

        var action = () => context.SaveChangesAsync(cancellationToken);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
        var postgres = PostgresException(exception);
        Assert.Equal((PostgresConstraintTranslator.UniqueViolation,
                "uq_client_supplement_active"),
            (postgres.SqlState, postgres.ConstraintName));
    }

    private static Domain.Entities.Supplements.ClientSupplementAssignment Assignment(
        PostgresContainerFixture.TestTenantSeed tenant, Guid supplementId) => new(
            tenant.TrainerId, tenant.ClientId, supplementId,
            "5 g", "daily", null, Now);

    private static Npgsql.PostgresException PostgresException(Exception exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is Npgsql.PostgresException postgres)
                return postgres;
            current = current.InnerException;
        }

        throw new InvalidOperationException("Expected a PostgreSQL exception.");
    }
}
