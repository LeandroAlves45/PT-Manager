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
/// Prova que o OutboxDispatcher só reclama tipos com consumidor registado.
/// </summary>
/// <remarks>
/// Substitui <c>OutboxDispatcherReservedTypesTests</c>, cuja premissa —
/// <c>trainer-logo.delete</c> sem dono — deixou de ser verdadeira na Fase 5C. A
/// intenção sobrevive com tipos genuinamente órfãos. A cobertura da composição
/// real (quais tipos têm consumidor em produção) vive em
/// <c>JobDispatchCompositionTests</c>, que monta o host verdadeiro: um provider
/// construído à mão aqui continuaria verde mesmo que um handler real faltasse.
/// </remarks>
[Collection(PostgresCollection.Name)]
public sealed class OutboxDispatcherHandlerRoutingTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 9, 11, 14, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public OutboxDispatcherHandlerRoutingTests(PostgresContainerFixture fixture) =>
        _fixture = fixture;

    public async ValueTask InitializeAsync() =>
        await _fixture.ExecuteSqlAsync(
            "DELETE FROM outbox_messages",
            TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Dispatch_WhenNoHandlerIsRegistered_ClaimsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owned = IntegrationTestData.Message(Now, messageType: "phase5a_test");
        var orphan = IntegrationTestData.Message(Now, messageType: "unowned_type");
        await SeedAsync(cancellationToken, owned, orphan);

        await using var provider = CreateProvider();
        await CreateDispatcher(provider).DispatchAsync(cancellationToken);

        await AssertPendingAsync(cancellationToken, owned.Id, orphan.Id);
    }

    [Fact]
    public async Task Dispatch_ClaimsOnlyTypesWithARegisteredHandler()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owned = IntegrationTestData.Message(Now, messageType: "phase5a_test");
        var logo = IntegrationTestData.Message(Now, messageType: "trainer-logo.delete");
        var avatar = IntegrationTestData.Message(Now, messageType: "client-avatar.delete");
        var orphan = IntegrationTestData.Message(Now, messageType: "unowned_type");
        await SeedAsync(cancellationToken, owned, logo, avatar, orphan);

        await using var provider = CreateProvider(new CompletingOutboxHandler());
        await CreateDispatcher(provider).DispatchAsync(cancellationToken);

        await using var context = _fixture.CreateAdministrativeContext();
        var ownedStored = await context.OutboxMessages.SingleAsync(
            message => message.Id == owned.Id, cancellationToken);
        Assert.NotEqual(JobStatus.Pending, ownedStored.Status);

        // Os tipos de media só são reclamados quando os seus consumidores estão
        // registados; aqui não estão, e ficam intactos para o dono legítimo.
        await AssertPendingAsync(cancellationToken, logo.Id, avatar.Id, orphan.Id);
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
