using Application.Features.Supplements.Abstractions;
using Domain.Entities.Administration;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Errors;
using Infrastructure.Persistence.Supplements;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Supplements;

[Collection(PostgresCollection.Name)]
public sealed class GlobalSupplementPersistenceTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public GlobalSupplementPersistenceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_WritesSupplementAndAuditInSameTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalSupplementStore(
            context, new PostgresConstraintTranslator());

        var outcome = await store.CreateAsync(
            tenant.TrainerId, "Creatine", null, "grams", "5 g",
            "daily", "internal", Now, cancellationToken);

        Assert.Equal(GlobalSupplementStoreResult.Status.Created, outcome.Kind);
        Assert.True(await context.AdministrativeAuditEntries.AnyAsync(
            entry => entry.ResourceId == outcome.Supplement!.Id &&
                entry.Action == "create", cancellationToken));
    }

    [Fact]
    public async Task Update_WhenSupplementIsArchived_ReturnsInactiveAndSkipsAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalSupplementStore(
            context, new PostgresConstraintTranslator());
        var created = await store.CreateAsync(
            tenant.TrainerId, "Creatine", null, "grams", "5 g",
            "daily", "internal", Now, cancellationToken);
        await store.SetActiveAsync(
            tenant.TrainerId, created.Supplement!.Id, false,
            Now.AddMinutes(1), cancellationToken);

        var outcome = await store.UpdateAsync(
            tenant.TrainerId, created.Supplement.Id, "Creatine Monohydrate", null,
            "grams", "5 g", "daily", "internal", Now.AddMinutes(2), cancellationToken);

        Assert.Equal(GlobalSupplementStoreResult.Status.Inactive, outcome.Kind);
        Assert.False(await context.AdministrativeAuditEntries.AnyAsync(
            entry => entry.ResourceId == created.Supplement.Id &&
                entry.Action == "update", cancellationToken));
    }

    [Fact]
    public async Task Delete_WhenSupplementHasAssignment_ReturnsConflictAndPreservesRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        Guid supplementId;
        await using (var admin = CreateAdminContext(tenant.TrainerId))
        {
            var globalStore = new GlobalSupplementStore(
                admin, new PostgresConstraintTranslator());
            var created = await globalStore.CreateAsync(
                tenant.TrainerId, "Creatine", null, "grams", "5 g",
                "daily", null, Now, cancellationToken);
            supplementId = created.Supplement!.Id;
        }
        await using (var trainer = _fixture.CreateContext(tenant.TrainerId))
        {
            var assignmentStore = new ClientSupplementAssignmentStore(
                trainer, new PostgresConstraintTranslator());
            await assignmentStore.AssignAsync(
                tenant.TrainerId, tenant.ClientId, supplementId,
                null, null, null, Now, cancellationToken);
        }
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalSupplementStore(
            context, new PostgresConstraintTranslator());

        var outcome = await store.DeleteAsync(
            tenant.TrainerId, supplementId, Now.AddMinutes(1), cancellationToken);

        Assert.Equal(GlobalSupplementStoreResult.Status.HasReferences, outcome.Kind);
        Assert.True(await context.Supplements.IgnoreQueryFilters()
            .AnyAsync(item => item.Id == supplementId, cancellationToken));
    }

    [Fact]
    public async Task Delete_WhenUnreferenced_RemovesSupplementButPreservesAuditHistory()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalSupplementStore(
            context, new PostgresConstraintTranslator());
        var created = await store.CreateAsync(
            tenant.TrainerId, "Creatine", null, "grams", "5 g",
            "daily", null, Now, cancellationToken);

        var outcome = await store.DeleteAsync(
            tenant.TrainerId, created.Supplement!.Id,
            Now.AddMinutes(1), cancellationToken);

        Assert.Equal(GlobalSupplementStoreResult.Status.Deleted, outcome.Kind);
        Assert.False(await context.Supplements.IgnoreQueryFilters()
            .AnyAsync(item => item.Id == created.Supplement.Id, cancellationToken));
        Assert.Equal(2, await context.AdministrativeAuditEntries.CountAsync(
            entry => entry.ResourceId == created.Supplement.Id, cancellationToken));
    }

    [Fact]
    public async Task DeleteAndAssign_WhenTheyRace_CannotLeaveAnOrphanReference()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        Guid supplementId;
        await using (var seed = CreateAdminContext(tenant.TrainerId))
        {
            var store = new GlobalSupplementStore(
                seed, new PostgresConstraintTranslator());
            var created = await store.CreateAsync(
                tenant.TrainerId, "Creatine", null, "grams", "5 g",
                "daily", null, Now, cancellationToken);
            supplementId = created.Supplement!.Id;
        }
        await using var deleteContext = CreateAdminContext(tenant.TrainerId);
        await using var assignContext = _fixture.CreateContext(tenant.TrainerId);
        var deleteStore = new GlobalSupplementStore(
            deleteContext, new PostgresConstraintTranslator());
        var assignmentStore = new ClientSupplementAssignmentStore(
            assignContext, new PostgresConstraintTranslator());

        var deleteTask = deleteStore.DeleteAsync(
            tenant.TrainerId, supplementId, Now.AddMinutes(1), cancellationToken);
        var assignTask = assignmentStore.AssignAsync(
            tenant.TrainerId, tenant.ClientId, supplementId,
            null, null, null, Now.AddMinutes(1), cancellationToken);
        await Task.WhenAll(deleteTask, assignTask);
        var deleteOutcome = await deleteTask;
        var assignOutcome = await assignTask;

        var validOutcomes =
            (deleteOutcome.Kind == GlobalSupplementStoreResult.Status.Deleted &&
             assignOutcome.Kind == ClientSupplementAssignmentStoreResult.Status.SupplementNotFound) ||
            (deleteOutcome.Kind == GlobalSupplementStoreResult.Status.HasReferences &&
             assignOutcome.Kind == ClientSupplementAssignmentStoreResult.Status.Assigned);
        Assert.True(validOutcomes);

        await using var verification = CreateAdminContext(tenant.TrainerId);
        var orphanCount = await verification.ClientSupplementAssignments
            .IgnoreQueryFilters()
            .Where(assignment => assignment.SupplementId == supplementId)
            .CountAsync(cancellationToken);
        if (orphanCount > 0)
        {
            Assert.True(await verification.Supplements.IgnoreQueryFilters()
                .AnyAsync(item => item.Id == supplementId, cancellationToken));
        }
    }

    [Fact]
    public async Task SaveChanges_WhenAuditIsModifiedOrDeleted_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = CreateAdminContext(tenant.TrainerId);
        var entry = new AdministrativeAuditEntry(
            tenant.TrainerId, "create", "supplement", Guid.NewGuid(),
            null, "{}", Now);
        context.AdministrativeAuditEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
        context.AdministrativeAuditEntries.Remove(entry);

        var action = () => context.SaveChangesAsync(cancellationToken);

        await Assert.ThrowsAsync<Domain.Exceptions.DomainException>(action);
    }

    private PtManagerDbContext CreateAdminContext(Guid actorUserId)
    {
        var tenantContext = TestTenantContext.Administrator(actorUserId);
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .AddInterceptors(new TenantWriteValidationInterceptor(tenantContext))
            .Options;
        return new PtManagerDbContext(options, tenantContext);
    }
}
