using Application.Common.Abstractions;
using Application.Features.Jobs.Abstractions;
using Application.Features.Jobs.Dispatching;
using Domain.Entities.Jobs;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Jobs;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// Garante que a Fase 5A não consome tipos de outbox das fases seguintes.
/// Sem handler registado o dispatcher não reclama nada; com um handler local
/// só o tipo correspondente sai de <c>pending</c>.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OutboxDispatcherReservedTypesTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 9, 8, 14, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public OutboxDispatcherReservedTypesTests(PostgresContainerFixture fixture) =>
        _fixture = fixture;

    public async ValueTask InitializeAsync() =>
        await _fixture.ExecuteSqlAsync(
            "TRUNCATE TABLE outbox_messages",
            TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Dispatch_WhenNoHandlerIsRegistered_LeavesReservedTypesPending()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var billing = IntegrationTestData.Message(Now, messageType: "billing_notification");
        var logo = IntegrationTestData.Message(Now, messageType: "trainer-logo.delete");
        await SeedAsync(cancellationToken, billing, logo);

        await using var provider = CreateProvider();
        var dispatcher = CreateDispatcher(provider);
        await dispatcher.DispatchAsync(cancellationToken);

        await AssertPendingAsync(cancellationToken, billing.Id, logo.Id);
    }

    [Fact]
    public async Task Dispatch_WhenHandlerExists_DoesNotClaimReservedTypes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owned = IntegrationTestData.Message(Now, messageType: "phase5a_test");
        var billing = IntegrationTestData.Message(Now, messageType: "billing_notification");
        var logo = IntegrationTestData.Message(Now, messageType: "trainer-logo.delete");
        await SeedAsync(cancellationToken, owned, billing, logo);

        await using var provider = CreateProvider(new CompletingOutboxHandler());
        var dispatcher = CreateDispatcher(provider);
        await dispatcher.DispatchAsync(cancellationToken);

        await using var context = _fixture.CreateAdministrativeContext();
        var ownedStored = await context.OutboxMessages.SingleAsync(
            message => message.Id == owned.Id, cancellationToken);
        Assert.NotEqual(JobStatus.Pending, ownedStored.Status);
        await AssertPendingAsync(cancellationToken, billing.Id, logo.Id);
    }

    private async Task SeedAsync(
        CancellationToken cancellationToken,
        params OutboxMessage[] messages)
    {
        await using var context = _fixture.CreateAdministrativeContext();
        context.OutboxMessages.AddRange(messages);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task AssertPendingAsync(
        CancellationToken cancellationToken,
        params Guid[] ids)
    {
        await using var context = _fixture.CreateAdministrativeContext();
        var stored = await context.OutboxMessages
            .Where(message => ids.Contains(message.Id))
            .ToListAsync(cancellationToken);

        Assert.Equal(ids.Length, stored.Count);
        Assert.All(stored, message => Assert.Equal(JobStatus.Pending, message.Status));
    }

    private ServiceProvider CreateProvider(IOutboxMessageHandler? handler = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new TestClock(Now));
        services.AddSingleton(Options.Create(new JobDispatchOptions()));
        services.AddScoped(_ => _fixture.CreateAdministrativeContext());
        services.AddScoped<IOutboxStore, OutboxRepository>();
        services.AddScoped<JobTenantValidator>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContextInitializer>(
            provider => provider.GetRequiredService<TenantContext>());
        if (handler is not null)
            services.AddSingleton(handler);

        return services.BuildServiceProvider();
    }

    private static OutboxDispatcher CreateDispatcher(IServiceProvider provider) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<JobDispatchOptions>>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<ILogger<OutboxDispatcher>>());

    private sealed class CompletingOutboxHandler : IOutboxMessageHandler
    {
        public string MessageType => "phase5a_test";

        public Task<DispatchItemOutcome> HandleAsync(
            OutboxMessageEnvelope message,
            CancellationToken cancellationToken) =>
            Task.FromResult(DispatchItemOutcome.Succeeded());
    }
}
