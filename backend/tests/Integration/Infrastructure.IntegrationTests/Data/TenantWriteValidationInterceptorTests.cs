using Domain.Entities.Clients;
using Domain.Entities.Supplements;
using Domain.Exceptions;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;

namespace Infrastructure.IntegrationTests.Data;

[Collection(PostgresCollection.Name)]
public sealed class TenantWriteValidationInterceptorTests
{
    private static readonly DateTime Now = new(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public TenantWriteValidationInterceptorTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveChanges_WhenTenantIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var client = CreateClient(tenant.TrainerId);

        await using var context = _fixture.CreateContext(trainerId: null);
        context.Clients.Add(client);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        // RequireTenant() lança InvalidOperationException, não DomainException:
        // ausência de tenant é um erro de configuração do chamador, não uma
        // violação de regra de negócio (ver PtManagerDbContext.RequireTenant).
        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task SaveChanges_WhenOwnerDiffersFromTenant_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = await _fixture.SeedTenantWithClientAsync(
            $"owner-{Guid.NewGuid():N}", cancellationToken);
        var requester = await _fixture.SeedTenantWithClientAsync(
            $"requester-{Guid.NewGuid():N}", cancellationToken);
        var client = CreateClient(owner.TrainerId);

        await using var context = _fixture.CreateContext(requester.TrainerId);
        context.Clients.Add(client);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task SaveChanges_WhenSynchronousApiIsUsed_ThrowsInvalidOperationException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(tenant.TrainerId, Now);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.Supplements.Add(supplement);

        // Act
        Action action = () => context.SaveChanges();

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    private static Client CreateClient(Guid ownerTrainerId) =>
        new(
            ownerTrainerId,
            "Interceptor Test Client",
            $"client-{Guid.NewGuid():N}@example.test",
            "+351900000000",
            BirthDate.Create(new DateOnly(1995, 1, 1), DateOnly.FromDateTime(Now)),
            BiologicalSex.Male,
            null,
            null,
            null,
            null,
            Now);
}
