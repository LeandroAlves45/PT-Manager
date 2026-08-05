using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.IntegrationTests.Tenancy;

[Collection(PostgresCollection.Name)]
public sealed class TenantQueryFilterTests
{
    private readonly PostgresContainerFixture _fixture;

    public TenantQueryFilterTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Clients_WhenReadByAnotherTenant_AreInvisible()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantA = await _fixture.SeedTenantWithClientAsync(
            $"a-{Guid.NewGuid():N}", cancellationToken);
        var tenantB = await _fixture.SeedTenantWithClientAsync(
            $"b-{Guid.NewGuid():N}", cancellationToken);

        await using var context = _fixture.CreateContext(tenantA.TrainerId);

        // Act
        var visible = await context.Clients
            .AnyAsync(client => client.Id == tenantB.ClientId, cancellationToken);

        // Assert
        Assert.False(visible);
    }

    [Fact]
    public async Task Clients_WhenNoTenantIsEstablished_AreInvisible()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = _fixture.CreateContext(null);

        // Act
        var visible = await context.Clients
            .AnyAsync(client => client.Id == tenant.ClientId, cancellationToken);

        // Assert
        Assert.False(visible);
    }

    [Fact]
    public async Task Client_WhenSoftDeleted_IsInvisibleToOwner()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var now = new DateTime(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc);

        await using (var writeContext = _fixture.CreateContext(tenant.TrainerId))
        {
            var client = await writeContext.Clients
                .SingleAsync(value => value.Id == tenant.ClientId, cancellationToken);
            client.SoftDelete(now);
            await writeContext.SaveChangesAsync(cancellationToken);
        }

        await using var readContext = _fixture.CreateContext(tenant.TrainerId);

        // Act
        var visible = await readContext.Clients
            .AnyAsync(client => client.Id == tenant.ClientId, cancellationToken);

        // Assert
        Assert.False(visible);
    }
}
