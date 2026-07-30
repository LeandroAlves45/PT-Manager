using Application.Common.Abstractions;
using System;

namespace Infrastructure.Data;

/// <summary>
/// Implementação scoped de ITenantContext. É composta pela origem do pedido
/// (middleware HTTP, dispatcher de jobs, handlers de webhooks) e depois lida.
/// </summary>
internal sealed class TenantContext : ITenantContext
{
    public Guid? TrainerId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Role { get; private set; }
    public TenantOrigin Origin { get; private set; } = TenantOrigin.System;
    public bool IsAdministrative { get; private set; }

    private bool _established;

    /// <summary>
    /// Define o tenant efetivo a partir de claims já validadadas. Só pode ser chamado uma vez
    /// por scope.
    /// </summary>
    public void Establish(
        Guid? trainerId,
        Guid? userId,
        string? role,
        TenantOrigin origin,
        bool isAdministrative
    )
    {
        if (_established)
            throw new InvalidOperationException("Tenant context is already established");

        TrainerId = trainerId;
        UserId = userId;
        Role = role;
        Origin = origin;
        IsAdministrative = isAdministrative;
        _established = true;
    }
}
