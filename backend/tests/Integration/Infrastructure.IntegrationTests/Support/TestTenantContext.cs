using Application.Common.Abstractions;

namespace Infrastructure.IntegrationTests.Support;

/// <summary>
/// Contexto imutável por instância. Cada DbContext recebe o tenant esperado,
/// evitando que um teste altere o tenant de outro em execução paralela.
/// </summary>
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

    public static TestTenantContext ForTrainer(Guid trainerId) =>
        new(trainerId, role: "trainer", origin: TenantOrigin.Http);

    public static TestTenantContext Administrator() =>
        new(null, role: "superuser", origin: TenantOrigin.System, isAdministrative: true);

    public static TestTenantContext WithoutTenant() =>
        new(null, origin: TenantOrigin.System);
}
