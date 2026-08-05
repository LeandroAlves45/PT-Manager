using Domain.Entities.Supplements;
using Domain.Exceptions;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.IntegrationTests.Catalogs;

[Collection(PostgresCollection.Name)]
public sealed class ClientSupplementAssignmentTests
{
    private static readonly DateTime Now = new(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public ClientSupplementAssignmentTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveChanges_WhenGlobalSupplementIsReferenced_PersistsAssignment()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(null, Now);

        await using (var adminContext = _fixture.CreateAdministrativeContext())
        {
            adminContext.Supplements.Add(supplement);
            await adminContext.SaveChangesAsync(cancellationToken);
        }

        var assignment = CreateAssignment(tenant, supplement.Id);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.ClientSupplementAssignments.Add(assignment);

        // Act
        await context.SaveChangesAsync(cancellationToken);

        // Assert
        Assert.True(await context.ClientSupplementAssignments
            .AnyAsync(value => value.Id == assignment.Id, cancellationToken));
    }

    [Fact]
    public async Task SaveChanges_WhenPrivateSupplementBelongsToTenant_PersistsAssignment()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(tenant.TrainerId, Now);
        var assignment = CreateAssignment(tenant, supplement.Id);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.Supplements.Add(supplement);
        context.ClientSupplementAssignments.Add(assignment);

        // Act
        await context.SaveChangesAsync(cancellationToken);

        // Assert
        Assert.True(await context.ClientSupplementAssignments
            .AnyAsync(value => value.Id == assignment.Id, cancellationToken));
    }

    [Fact]
    public async Task SaveChanges_WhenPrivateSupplementBelongsToAnotherTenant_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = await _fixture.SeedTenantWithClientAsync(
            $"owner-{Guid.NewGuid():N}", cancellationToken);
        var requester = await _fixture.SeedTenantWithClientAsync(
            $"requester-{Guid.NewGuid():N}", cancellationToken);
        var supplement = IntegrationTestData.Supplement(owner.TrainerId, Now);

        await using (var ownerContext = _fixture.CreateContext(owner.TrainerId))
        {
            ownerContext.Supplements.Add(supplement);
            await ownerContext.SaveChangesAsync(cancellationToken);
        }

        await using var context = _fixture.CreateContext(requester.TrainerId);
        context.ClientSupplementAssignments.Add(CreateAssignment(requester, supplement.Id));

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task SaveChanges_WhenClientBelongsToAnotherTenant_CompositeFkRejectsWrite()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantA = await _fixture.SeedTenantWithClientAsync(
            $"a-{Guid.NewGuid():N}", cancellationToken);
        var tenantB = await _fixture.SeedTenantWithClientAsync(
            $"b-{Guid.NewGuid():N}", cancellationToken);
        var supplement = IntegrationTestData.Supplement(tenantA.TrainerId, Now);

        await using (var seedContext = _fixture.CreateContext(tenantA.TrainerId))
        {
            seedContext.Supplements.Add(supplement);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        var invalid = new ClientSupplementAssignment(
            tenantA.TrainerId,
            tenantB.ClientId,
            supplement.Id,
            "5 g",
            "Daily",
            null,
            Now);

        await using var context = _fixture.CreateContext(tenantA.TrainerId);
        context.ClientSupplementAssignments.Add(invalid);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
        var postgres = exception.InnerException as PostgresException
            ?? throw new InvalidOperationException("Expected PostgreSQL error.");
        Assert.Equal(
            (PostgresErrorCodes.ForeignKeyViolation,
                "fk_client_supplement_assignments_client_tenant"),
            (postgres.SqlState, postgres.ConstraintName));
    }

    [Fact]
    public async Task SaveChanges_WhenActiveAssignmentAlreadyExists_UniqueIndexRejectsSecond()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(tenant.TrainerId, Now);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.Supplements.Add(supplement);
        context.ClientSupplementAssignments.Add(CreateAssignment(tenant, supplement.Id));
        await context.SaveChangesAsync(cancellationToken);
        context.ClientSupplementAssignments.Add(CreateAssignment(tenant, supplement.Id));

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
        var postgres = exception.InnerException as PostgresException
            ?? throw new InvalidOperationException("Expected PostgreSQL error.");

        Assert.Equal(
            (PostgresErrorCodes.UniqueViolation,
                "uq_client_supplement_active"),
            (postgres.SqlState, postgres.ConstraintName));
    }

    [Fact]
    public async Task SaveChanges_WhenPreviousAssignmentIsSoftDeleted_AllowsReplacement()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(tenant.TrainerId, Now);
        var previous = CreateAssignment(tenant, supplement.Id);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.Supplements.Add(supplement);
        context.ClientSupplementAssignments.Add(previous);
        await context.SaveChangesAsync(cancellationToken);
        previous.SoftDelete(Now.AddMinutes(1));
        await context.SaveChangesAsync(cancellationToken);
        context.ClientSupplementAssignments.Add(CreateAssignment(tenant, supplement.Id));

        // Act
        await context.SaveChangesAsync(cancellationToken);
        var activeCount = await context.ClientSupplementAssignments
            .CountAsync(value => value.ClientId == tenant.ClientId, cancellationToken);

        // Assert
        Assert.Equal(1, activeCount);
    }

    private static ClientSupplementAssignment CreateAssignment(
        PostgresContainerFixture.TestTenantSeed tenant,
        Guid supplementId) =>
        new(tenant.TrainerId, tenant.ClientId, supplementId, "5 g", "Daily", null, Now);
}
