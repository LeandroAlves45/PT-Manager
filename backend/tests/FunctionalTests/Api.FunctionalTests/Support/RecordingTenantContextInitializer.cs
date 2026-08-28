using Application.Common.Abstractions;

namespace Api.FunctionalTests.Support;

/// <summary>Regista a identidade efetiva estabelecida pelo middleware de tenant.</summary>
internal sealed class RecordingTenantContextInitializer : ITenantContextInitializer
{
    public bool WasEstablished { get; private set; }
    public Guid? TrainerId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Role { get; private set; }
    public TenantOrigin Origin { get; private set; }
    public bool IsAdministrative { get; private set; }

    public void Establish(
        Guid? trainerId,
        Guid? userId,
        string? role,
        TenantOrigin origin,
        bool isAdministrative)
    {
        WasEstablished = true;
        TrainerId = trainerId;
        UserId = userId;
        Role = role;
        Origin = origin;
        IsAdministrative = isAdministrative;
    }
}
