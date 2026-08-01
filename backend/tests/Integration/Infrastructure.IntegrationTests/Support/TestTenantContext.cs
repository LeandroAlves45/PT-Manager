using Application.Common.Abstractions;

namespace Infrastructure.IntegrationTests.Support;

/// <summary>
/// Contexto imutável por instância. Cada DbContext recebe o tenant esperado,
/// evitanto que um teste altere o tenant de outro em execução paralela.
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
}
