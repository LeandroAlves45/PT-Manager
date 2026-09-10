using Api.FunctionalTests.Support;
using Application.Features.Jobs.Dispatching;
using Application.Features.Notifications.Delivery;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Contract;

/// <summary>
/// Verifica o composition root da execução durável.
/// O routing é resolvido por tipo e versão a partir do contentor. Dois handlers
/// registados para a mesma rota fariam o dispatcher lançar em runtime, no meio
/// de um job já reclamado; um handler em falta só apareceria quando o primeiro
/// job desse tipo fosse processado. Ambos os casos têm de ser detectados aqui.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class JobDispatchCompositionTests
{
    private readonly PostgresApiFixture _fixture;

    public JobDispatchCompositionTests(PostgresApiFixture fixture) => _fixture = fixture;

    [Fact]
    public void CompositionRoot_ResolvesEveryDispatchDependency()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        Assert.NotNull(provider.GetRequiredService<IJobDispatchActivation>());
        Assert.NotNull(provider.GetRequiredService<IInternalDispatchRequestAuthenticator>());
        Assert.NotNull(provider.GetRequiredService<INotificationDeliveryStore>());
        Assert.NotNull(provider.GetRequiredService<INotificationDeliveryGateway>());
    }

    [Fact]
    public void DurableJobHandlers_HaveNoDuplicateRoute()
    {
        using var scope = _fixture.Factory.Services.CreateScope();

        var routes = scope.ServiceProvider
            .GetServices<IDurableJobHandler>()
            .Select(handler => $"{handler.JobType}:v{handler.JobVersion}")
            .ToArray();

        Assert.Equal(routes.Length, routes.Distinct().Count());
    }

    [Fact]
    public void SendNotification_IsTheOnlyRegisteredDurableJobRoute()
    {
        using var scope = _fixture.Factory.Services.CreateScope();

        var handlers = scope.ServiceProvider.GetServices<IDurableJobHandler>().ToArray();

        var handler = Assert.Single(handlers);
        Assert.Equal("send_notification", handler.JobType);
        Assert.Equal(1, handler.JobVersion);
    }

    [Fact]
    public void BillingNotification_IsTheOnlyRegisteredOutboxRoute()
    {
        using var scope = _fixture.Factory.Services.CreateScope();

        var handler = Assert.Single(
            scope.ServiceProvider.GetServices<IOutboxMessageHandler>());
        Assert.Equal("billing_notification", handler.MessageType);
    }

    [Fact]
    public void DeliveryStore_IsScopedSoEachItemGetsItsOwnDbContext()
    {
        // Um DbContext partilhado entre itens concorrentes seria usado em paralelo,
        // que é precisamente o que o desenho por scope evita.
        using var first = _fixture.Factory.Services.CreateScope();
        using var second = _fixture.Factory.Services.CreateScope();

        var storeOne = first.ServiceProvider.GetRequiredService<INotificationDeliveryStore>();
        var storeTwo = second.ServiceProvider.GetRequiredService<INotificationDeliveryStore>();

        Assert.NotSame(storeOne, storeTwo);
    }

    [Fact]
    public void Activation_IsSingletonAcrossScopes()
    {
        using var first = _fixture.Factory.Services.CreateScope();
        using var second = _fixture.Factory.Services.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<IJobDispatchActivation>(),
            second.ServiceProvider.GetRequiredService<IJobDispatchActivation>());
    }
}
