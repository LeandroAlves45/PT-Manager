using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Clients;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.TrainerSettings;

[Collection(PostgresCollection.Name)]
public sealed class ClientBrandingPersistenceTests
{
    private static readonly DateTime Now =
        new(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public ClientBrandingPersistenceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_WhenClientIsActiveWithoutCustomLogo_ReturnsNullLogoForFrontendFallback()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var settings = await context.TrainerSettings.SingleAsync(cancellationToken);
        settings.UpdateBranding("Studio Fit", "#112233", "#445566", Now);
        await context.SaveChangesAsync(cancellationToken);
        var queries = new ClientBrandingQueries(context);

        var result = await queries.GetAsync(
            tenant.TrainerId,
            tenant.ClientUserId,
            cancellationToken);

        Assert.Equal("Studio Fit", result!.AppName);
        Assert.Null(result.LogoUrl);
    }

    [Fact]
    public async Task Get_WhenClientIsArchived_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var client = await context.Clients.SingleAsync(
            candidate => candidate.Id == tenant.ClientId,
            cancellationToken);
        client.Deactivate(Now);
        await context.SaveChangesAsync(cancellationToken);
        var queries = new ClientBrandingQueries(context);

        var result = await queries.GetAsync(
            tenant.TrainerId,
            tenant.ClientUserId,
            cancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Get_WithAnotherTrainerContext_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = await _fixture.SeedTenantWithClientAsync(
            $"owner-{Guid.NewGuid():N}", cancellationToken);
        var requester = await _fixture.SeedTenantWithClientAsync(
            $"requester-{Guid.NewGuid():N}", cancellationToken);
        await using var context = _fixture.CreateContext(requester.TrainerId);
        var queries = new ClientBrandingQueries(context);

        var result = await queries.GetAsync(
            owner.TrainerId,
            owner.ClientUserId,
            cancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Get_WhenTrainerSettingsAreMissing_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        await context.TrainerSettings.ExecuteDeleteAsync(cancellationToken);
        var queries = new ClientBrandingQueries(context);

        var result = await queries.GetAsync(
            tenant.TrainerId,
            tenant.ClientUserId,
            cancellationToken);

        Assert.Null(result);
    }
}
