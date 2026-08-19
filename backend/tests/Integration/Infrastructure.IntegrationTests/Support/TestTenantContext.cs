using Application.Common.Abstractions;

namespace Infrastructure.IntegrationTests.Support;

/// <summary>Contexto imutável por DbContext para impedir fuga de tenant entre testes.</summary>
internal sealed class TestTenantContext : ITenantContext
{
    public Guid? TrainerId { get; }
    public Guid? UserId { get; }
    public string? Role { get; }
    public TenantOrigin Origin { get; }
    public bool IsAdministrative { get; }

    public TestTenantContext(
        Guid? trainerId,
        Guid? userId = null,
        string? role = null,
        TenantOrigin origin = TenantOrigin.System,
        bool isAdministrative = false)
    {
        TrainerId = trainerId;
        UserId = userId;
        Role = role;
        Origin = origin;
        IsAdministrative = isAdministrative;
    }

    public static TestTenantContext ForTrainer(Guid trainerId, Guid? userId = null) =>
        new(trainerId, userId, "trainer", TenantOrigin.Http);

    public static TestTenantContext Administrator(Guid actorUserId) =>
        new(null, actorUserId, "superuser", TenantOrigin.Http, true);

    public static TestTenantContext Administrator() => Administrator(Guid.NewGuid());

    public static TestTenantContext WithoutTenant() =>
        new(null, origin: TenantOrigin.System);
}
