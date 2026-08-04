using Domain.Entities.Supplements;
using Domain.Exceptions;
using Infrastructure.IntegrationTests.Support;

namespace Infrastructure.IntegrationTests.Data;

public sealed class TenantWriteValidationInterceptorTests
    : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public TenantWriteValidationInterceptorTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveChanges_WhenAssignmentReferencesDeletedSupplement_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var discriminator = Guid.NewGuid().ToString("N");
        var tenant = await _fixture.SeedTenantWithClientAsync(discriminator, cancellationToken);
        var supplementId = await SeedDeletedSupplementAsync(tenant.TrainerId, cancellationToken);
        var assignment = CreateAssignment(tenant, supplementId);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.ClientSupplementAssignments.Add(assignment);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task SaveChanges_WhenAssignmentReferencesMissingSupplement_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var discriminator = Guid.NewGuid().ToString("N");
        var tenant = await _fixture.SeedTenantWithClientAsync(discriminator, cancellationToken);
        var assignment = CreateAssignment(tenant, Guid.NewGuid());

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.ClientSupplementAssignments.Add(assignment);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    private async Task<Guid> SeedDeletedSupplementAsync(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var now = new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);
        var supplement = new Supplement(
            trainerId,
            trainerId,
            "Deleted supplement",
            null,
            "capsules",
            "1 capsule",
            "Daily",
            null,
            now);

        await using var context = _fixture.CreateContext(trainerId);
        context.Supplements.Add(supplement);
        await context.SaveChangesAsync(cancellationToken);

        supplement.SoftDelete(now.AddMinutes(1));
        await context.SaveChangesAsync(cancellationToken);

        return supplement.Id;
    }

    private static ClientSupplementAssignment CreateAssignment(
        PostgresContainerFixture.TestTenantSeed tenant,
        Guid supplementId) =>
        new(
            tenant.TrainerId,
            tenant.ClientId,
            supplementId,
            "1 capsule",
            "With breakfast",
            null,
            new DateTime(2026, 8, 4, 12, 5, 0, DateTimeKind.Utc));
}
