using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Clients;

/// <summary>
/// Cria contextos independentes com a mesma execution strategy usada em produção.
/// </summary>
internal sealed class ClientStoreTestContextFactory
{
    private readonly string _connectionString;

    public ClientStoreTestContextFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));

        _connectionString = connectionString;
    }

    public PtManagerDbContext Create(Guid trainerId)
    {
        var tenantContext = TestTenantContext.ForTrainer(trainerId);
        var interceptor = new TenantWriteValidationInterceptor(tenantContext);
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(
                _connectionString,
                npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3))
            .AddInterceptors(interceptor)
            .EnableDetailedErrors()
            .Options;

        return new PtManagerDbContext(options, tenantContext);
    }
}
