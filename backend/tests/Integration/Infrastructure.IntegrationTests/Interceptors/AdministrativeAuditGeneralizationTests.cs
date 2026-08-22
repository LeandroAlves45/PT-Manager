using Domain.Entities.Nutrition;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Interceptors;

[Collection(PostgresCollection.Name)]
public sealed class AdministrativeAuditGeneralizationTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public AdministrativeAuditGeneralizationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveChanges_WhenGlobalFoodWrittenWithoutAuditEntry_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var tenantContext = TestTenantContext.Administrator(tenant.TrainerId);
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .AddInterceptors(new TenantWriteValidationInterceptor(tenantContext))
            .Options;
        await using var context = new PtManagerDbContext(options, tenantContext);
        context.Foods.Add(new Food(null, "Rice", null, 2.7m, 28m, 0.3m, 0.4m, Now));

        var action = () => context.SaveChangesAsync(cancellationToken);

        await Assert.ThrowsAsync<Domain.Exceptions.DomainException>(action);
    }

    [Fact]
    public async Task SaveChanges_WhenGlobalFoodWrittenWithMatchingAuditEntry_Succeeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var tenantContext = TestTenantContext.Administrator(tenant.TrainerId);
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .AddInterceptors(new TenantWriteValidationInterceptor(tenantContext))
            .Options;
        await using var context = new PtManagerDbContext(options, tenantContext);
        var food = new Food(null, "Rice", null, 2.7m, 28m, 0.3m, 0.4m, Now);
        context.Foods.Add(food);
        context.AdministrativeAuditEntries.Add(new Domain.Entities.Administration.AdministrativeAuditEntry(
            tenant.TrainerId, "create", "food", food.Id, null, "{}", Now));

        await context.SaveChangesAsync(cancellationToken);

        Assert.True(await context.Foods.IgnoreQueryFilters()
            .AnyAsync(item => item.Id == food.Id, cancellationToken));
    }
}
