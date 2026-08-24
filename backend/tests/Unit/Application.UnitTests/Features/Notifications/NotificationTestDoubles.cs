using Application.Common.Abstractions;
using Application.Features.Notifications.Abstractions;

namespace Application.UnitTests.Features.Notifications;

/// <summary>Relógio determinístico dos testes de Notifications.</summary>
internal sealed class StubClock : IClock
{
    public DateTime UtcNow { get; init; }
}

/// <summary>Contexto tenant configurável sem dependências externas.</summary>
internal sealed class StubTenantContext : ITenantContext
{
    public Guid? TrainerId { get; init; }
    public Guid? UserId { get; init; } = Guid.NewGuid();
    public string? Role { get; init; } = "trainer";
    public TenantOrigin Origin { get; init; } = TenantOrigin.Http;
    public bool IsAdministrative { get; init; }
}

/// <summary>Fake observável da porta de enqueue de notificações.</summary>
internal sealed class FakeNotificationQueueStore : INotificationQueueStore
{
    public NotificationQueueStoreResult Result { get; set; } =
        NotificationQueueStoreResult.Queued(Guid.NewGuid(), DateTime.UtcNow);

    public int Calls { get; private set; }
    public NotificationQueueRequest? LastRequest { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<NotificationQueueStoreResult> EnqueueAsync(
        NotificationQueueRequest request,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastRequest = request;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Result);
    }
}
