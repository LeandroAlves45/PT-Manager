using Api.FunctionalTests.Support;
using Application.Features.Jobs.Dispatching;
using Application.Features.Notifications.Delivery;
using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
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
    public void RegisteredDurableJobRoutes_AreTheClosedAllowlist()
    {
        using var scope = _fixture.Factory.Services.CreateScope();

        var routes = scope.ServiceProvider
            .GetServices<IDurableJobHandler>()
            .Select(handler => $"{handler.JobType}:v{handler.JobVersion}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "exercise-video.delete-object:v1",
                "exercise-video.expire:v1",
                "exercise-video.process:v1",
                "send_notification:v1"
            ],
            routes);
    }

    /// <summary>
    /// Os jobs de vídeo são escritos pelos stores com os tipos de
    /// <see cref="ExerciseVideoJobs"/>. Um tipo escrito sem handler registado
    /// terminaria em dead letter com job_handler_not_registered.
    /// </summary>
    [Fact]
    public void ExerciseVideoJobTypesWrittenByTheStores_HaveRegisteredPlatformHandlers()
    {
        using var scope = _fixture.Factory.Services.CreateScope();

        var platformRoutes = scope.ServiceProvider
            .GetServices<IDurableJobHandler>()
            .OfType<IPlatformDurableJobHandler>()
            .Select(handler => handler.JobType)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Superset(
            platformRoutes,
            new HashSet<string>(StringComparer.Ordinal)
            {
                ExerciseVideoJobs.ProcessType,
                ExerciseVideoJobs.ExpireType,
                ExerciseVideoJobs.DeleteObjectType
            });
    }

    [Fact]
    public void CompositionRoot_ResolvesEveryVideoDependency()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        Assert.NotNull(provider.GetRequiredService<IVideoObjectStorage>());
        Assert.NotNull(provider.GetRequiredService<IVideoMetadataProbe>());
        Assert.NotNull(provider.GetRequiredService<IExerciseVideoStore>());
        Assert.NotNull(provider.GetRequiredService<IExerciseVideoQueries>());
        Assert.NotNull(provider.GetRequiredService<IExerciseVideoProcessingStore>());
        Assert.NotNull(provider.GetRequiredService<ExerciseVideoSettings>());
    }

    /// <summary>
    /// A allowlist que o OutboxDispatcher passa ao claim SQL deriva destes
    /// registos. Tem de cobrir exatamente os tipos que os stores escrevem: um
    /// tipo escrito sem consumidor fica pending para sempre, e um consumidor sem
    /// produtor é superfície morta.
    /// </summary>
    [Fact]
    public void RegisteredOutboxRoutes_CoverEveryTypeWrittenByTheStores()
    {
        using var scope = _fixture.Factory.Services.CreateScope();

        var messageTypes = scope.ServiceProvider
            .GetServices<IOutboxMessageHandler>()
            .Select(handler => handler.MessageType)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["billing_notification", "client-avatar.delete", "trainer-logo.delete"],
            messageTypes);
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
